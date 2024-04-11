# Copyright (c) Microsoft. All rights reserved.
from __future__ import annotations

import importlib
import inspect
import json
import logging
import os
import sys
from collections.abc import Callable, Iterable
from glob import glob
from types import MethodType
from typing import TYPE_CHECKING, Any, ItemsView

if sys.version_info >= (3, 9):
    from typing import Annotated  # type: ignore
else:
    from typing_extensions import Annotated  # type: ignore

import httpx
import yaml
from pydantic import Field, StringConstraints

from semantic_kernel.connectors.openai_plugin.openai_authentication_config import OpenAIAuthenticationConfig
from semantic_kernel.connectors.openai_plugin.openai_utils import OpenAIUtils
from semantic_kernel.connectors.openapi_plugin.openapi_manager import OpenAPIPlugin
from semantic_kernel.connectors.utils.document_loader import DocumentLoader
from semantic_kernel.exceptions import KernelPluginInvalidConfigurationError, PluginInitializationError
from semantic_kernel.functions.kernel_function import TEMPLATE_FORMAT_MAP, KernelFunction
from semantic_kernel.functions.kernel_function_from_method import KernelFunctionFromMethod
from semantic_kernel.functions.kernel_function_from_prompt import KernelFunctionFromPrompt
from semantic_kernel.functions.types import KERNEL_FUNCTION_TYPE
from semantic_kernel.kernel_pydantic import KernelBaseModel
from semantic_kernel.prompt_template.prompt_template_config import PromptTemplateConfig
from semantic_kernel.utils.validation import PLUGIN_NAME_REGEX

if TYPE_CHECKING:
    from semantic_kernel.connectors.openai_plugin.openai_function_execution_parameters import (
        OpenAIFunctionExecutionParameters,
    )
    from semantic_kernel.connectors.openapi_plugin.openapi_function_execution_parameters import (
        OpenAPIFunctionExecutionParameters,
    )
    from semantic_kernel.functions.kernel_function_metadata import KernelFunctionMetadata

logger = logging.getLogger(__name__)


class KernelPlugin(KernelBaseModel):
    """
    Represents a Kernel Plugin with functions.

    This class behaves mostly like a dictionary, with functions as values and their names as keys.
    When you add a function, through `.set` or `__setitem__`, the function is copied, the metadata is deep-copied
    and the name of the plugin is set in the metadata and added to the dict of functions.
    This is done in the same way as a normal dict, so a existing key will be overwritten.

    Attributes:
        name (str): The name of the plugin. The name can be upper/lower
            case letters and underscores.
        description (str): The description of the plugin.
        functions (Dict[str, KernelFunction]): The functions in the plugin,
            indexed by their name.

    Methods:
        set, __setitem__ (key: str, value: KernelFunction): Set a function in the plugin.
        get (key: str, default: KernelFunction | None = None): Get a function from the plugin.
        __getitem__ (key: str): Get a function from the plugin.
        __contains__ (key: str): Check if a function is in the plugin.
        __iter__ (): Iterate over the functions in the plugin.
        update(*args: Any, **kwargs: Any): Update the plugin with the functions from another.
        setdefault(key: str, value: KernelFunction | None): Set a default value for a key.
        get_functions_metadata(): Get the metadata for the functions in the plugin.

    Class methods:
        from_object(plugin_name: str, plugin_instance: Any | dict[str, Any], description: str | None = None):
            Create a plugin from a existing object, like a custom class with annotated functions.
        from_directory(plugin_name: str, parent_directory: str, description: str | None = None):
            Create a plugin from a directory, parsing:
            .py files, .yaml files and directories with skprompt.txt and config.json files.
        from_openapi(
                plugin_name: str,
                openapi_document_path: str,
                execution_settings: OpenAPIFunctionExecutionParameters | None = None,
                description: str | None = None):
            Create a plugin from an OpenAPI document.
        from_openai(
                plugin_name: str,
                plugin_url: str | None = None,
                plugin_str: str | None = None,
                execution_parameters: OpenAIFunctionExecutionParameters | None = None,
                description: str | None = None):
            Create a plugin from the Open AI manifest.

    """

    name: Annotated[str, StringConstraints(pattern=PLUGIN_NAME_REGEX, min_length=1)]
    description: str | None = None
    functions: dict[str, KernelFunction] = Field(default_factory=dict)

    def __init__(
        self,
        name: str,
        description: str | None = None,
        functions: (
            KERNEL_FUNCTION_TYPE
            | KernelPlugin
            | list[KERNEL_FUNCTION_TYPE | KernelPlugin]
            | dict[str, KERNEL_FUNCTION_TYPE]
            | None
        ) = None,
    ):
        """Create a KernelPlugin

        Attributes:
            name (str): The name of the plugin. The name can be upper/lower
                case letters and underscores.
            description (str, optional): The description of the plugin.
            functions (
                    KernelFunction |
                    Callable |
                    list[KernelFunction | Callable | KernelPlugin] |
                    dict[str, KernelFunction | Callable] |
                    KernelPlugin |
                    None):
                The functions in the plugin, will be rewritten to a dictionary of functions.

        Raises:
            ValueError: If the functions are not of the correct type.
            PydanticError: If the name is not a valid plugin name.
        """
        super().__init__(
            name=name,
            description=description,
            functions=self._validate_functions(functions=functions, plugin_name=name),
        )

    # region Dict-like methods

    def __setitem__(self, key: str, value: KernelFunction) -> None:
        self.functions[key] = KernelPlugin._parse_or_copy(value, self.name)

    def set(self, key: str, value: KernelFunction) -> None:
        """Set a function in the plugin.

        Args:
            key (str): The name of the function.
            value (KernelFunction): The function to set.

        """
        self[key] = value

    def __getitem__(self, key: str) -> KernelFunction:
        return self.functions[key]

    def get(self, key: str, default: KernelFunction | None = None) -> KernelFunction | None:
        return self.functions.get(key, default)

    def update(self, *args: Any, **kwargs: KernelFunction) -> None:
        """Update the plugin with the functions from another.

        Args:
            *args: The functions to update the plugin with, can be a dict, list or KernelPlugin.
            **kwargs: The kernel functions to update the plugin with.

        """
        if len(args) > 1:
            raise TypeError("update expected at most 1 arguments, got %d" % len(args))
        if args:
            other = args[0]
            if isinstance(other, KernelPlugin):
                other = other.functions
            if not isinstance(other, (dict, list)):
                raise TypeError(f"Expected dict, KernelPlugin or list as arg, got {type(other)}")
            if isinstance(other, dict):
                for key in other:
                    self[key] = other[key]
            else:
                for item in other:
                    if isinstance(item, KernelFunction):
                        self[item.name] = item
                    elif isinstance(item, KernelPlugin):
                        for key in item.functions:
                            self[key] = item.functions[key]
        if kwargs:
            for key in kwargs:
                self[key] = kwargs[key]

    def setdefault(self, key: str, value: KernelFunction | None = None):
        if key not in self.functions:
            if value is None:
                raise ValueError("Value must be provided for new key.")
            self[key] = value
        return self[key]

    def __iter__(self) -> Iterable[KernelFunction]:
        for function in self.functions.values():
            yield function

    def __contains__(self, key: str) -> bool:
        return key in self.functions

    # endregion
    # region Properties

    def get_functions_metadata(self) -> list["KernelFunctionMetadata"]:
        """
        Get the metadata for the functions in the plugin.

        Returns:
            A list of KernelFunctionMetadata instances.
        """
        return [func.metadata for func in self]

    # endregion
    # region Class Methods

    @classmethod
    def from_object(
        cls, plugin_name: str, plugin_instance: Any | dict[str, Any], description: str | None = None
    ) -> "KernelPlugin":
        """
        Creates a plugin that wraps the specified target object and imports it into the kernel's plugin collection

        Args:
            plugin_instance (Any | dict[str, Any]): The plugin instance. This can be a custom class or a
                dictionary of classes that contains methods with the kernel_function decorator for one or
                several methods. See `TextMemoryPlugin` as an example.
            plugin_name (str): The name of the plugin. Allows chars: upper, lower ASCII and underscores.

        Returns:
            KernelPlugin: The imported plugin of type KernelPlugin.
        """
        functions: list[KernelFunction] = []
        candidates: list[tuple[str, MethodType]] | ItemsView[str, Any] = []

        if isinstance(plugin_instance, dict):
            candidates = plugin_instance.items()
        else:
            candidates = inspect.getmembers(plugin_instance, inspect.ismethod)
        # Read every method from the plugin instance
        functions = [
            KernelFunctionFromMethod(method=candidate, plugin_name=plugin_name)
            for _, candidate in candidates
            if hasattr(candidate, "__kernel_function__")
        ]
        return cls(name=plugin_name, description=description, functions=functions)  # type: ignore

    @classmethod
    def from_directory(
        cls,
        plugin_name: str,
        parent_directory: str,
        description: str | None = None,
    ) -> "KernelPlugin":
        """Create a plugin from a specified directory.

        This method does not recurse into subdirectories beyond one level deep from the specified plugin directory.
        For YAML files, function names are extracted from the content of the YAML files themselves (the name property).
        For directories, the function name is assumed to be the name of the directory. Each KernelFunction object is
        initialized with data parsed from the associated files and added to a list of functions that are then assigned
        to the created KernelPlugin object.
        A native_function.py file is parsed and imported as a plugin,
        other functions found are then added to this plugin.

        Example:
            Assuming a plugin directory structure as follows:
        MyPlugins/
            |--- pluginA.yaml
            |--- pluginB.yaml
            |--- native_function.py
            |--- Directory1/
                |--- skprompt.txt
                |--- config.json
            |--- Directory2/
                |--- skprompt.txt
                |--- config.json

            Calling `KernelPlugin.from_directory("MyPlugins", "/path/to")` will create a KernelPlugin object named
                "MyPlugins", containing KernelFunction objects for `pluginA.yaml`, `pluginB.yaml`,
                `Directory1`, and `Directory2`, each initialized with their respective configurations.
                And functions for anything within native_function.py.

        Args:
            plugin_name (str): The name of the plugin, this is the name of the directory within the parent directory
            parent_directory (str): The parent directory path where the plugin directory resides
            description (str | None): The description of the plugin

        Returns:
            KernelPlugin: The created plugin of type KernelPlugin.

        Raises:
            PluginInitializationError: If the plugin directory does not exist.
            PluginInvalidNameError: If the plugin name is invalid.
        """
        plugin_directory = os.path.join(parent_directory, plugin_name)
        plugin_directory = os.path.abspath(plugin_directory)

        if not os.path.exists(plugin_directory):
            raise PluginInitializationError(f"Plugin directory does not exist: {plugin_name}")

        functions: list[KernelFunction] = []

        for py_file in glob(os.path.join(plugin_directory, "*.py")):
            module_name = os.path.basename(py_file).replace(".py", "")
            spec = importlib.util.spec_from_file_location(module_name, py_file)
            module = importlib.util.module_from_spec(spec)
            assert spec.loader
            spec.loader.exec_module(module)

            for name, cls_instance in inspect.getmembers(module, inspect.isclass):
                if cls_instance.__module__ != module_name:
                    continue
                plugin = cls.from_object(
                    plugin_name=plugin_name, plugin_instance=getattr(module, name)(), description=description
                )
                functions.extend(plugin)

        # Handle YAML files at the root
        for yaml_file in glob(os.path.join(plugin_directory, "*.yaml")):
            with open(yaml_file, "r") as file:
                yaml_content = file.read()

                if not yaml_content:
                    logger.warning(f"Empty YAML file: {yaml_file}")
                    continue

                try:
                    data = yaml.safe_load(yaml_content)
                except yaml.YAMLError as exc:
                    raise PluginInitializationError(f"Error loading YAML: {exc}") from exc

                if not isinstance(data, dict):
                    raise PluginInitializationError("The YAML content must represent a dictionary")

                try:
                    prompt_template_config = PromptTemplateConfig(**data)
                except TypeError as exc:
                    raise PluginInitializationError(f"Error initializing PromptTemplateConfig: {exc}") from exc
                functions.append(
                    KernelFunctionFromPrompt(
                        function_name=prompt_template_config.name,
                        plugin_name=plugin_name,
                        description=prompt_template_config.description,
                        prompt_template_config=prompt_template_config,
                        template_format=prompt_template_config.template_format,
                    )
                )

        # Handle directories containing skprompt.txt and config.json
        for item in os.listdir(plugin_directory):
            item_path = os.path.join(plugin_directory, item)
            if not os.path.isdir(item_path):
                continue
            prompt_path = os.path.join(item_path, "skprompt.txt")
            config_path = os.path.join(item_path, "config.json")

            if os.path.exists(prompt_path) and os.path.exists(config_path):
                with open(config_path, "r") as config_file:
                    prompt_template_config = PromptTemplateConfig.from_json(config_file.read())
                prompt_template_config.name = item

                with open(prompt_path, "r") as prompt_file:
                    prompt = prompt_file.read()
                    prompt_template_config.template = prompt

                prompt_template = TEMPLATE_FORMAT_MAP[prompt_template_config.template_format](  # type: ignore
                    prompt_template_config=prompt_template_config
                )
                functions.append(
                    KernelFunctionFromPrompt(
                        plugin_name=plugin_name,
                        prompt_template=prompt_template,
                        prompt_template_config=prompt_template_config,
                        template_format=prompt_template_config.template_format,
                        function_name=item,
                        description=prompt_template_config.description,
                    )
                )
        if not functions:
            raise PluginInitializationError(f"No functions found in folder: {parent_directory}/{plugin_name}")
        return cls(name=plugin_name, description=description, functions=functions)

    @classmethod
    def from_openapi(
        cls,
        plugin_name: str,
        openapi_document_path: str,
        execution_settings: "OpenAPIFunctionExecutionParameters | None" = None,
        description: str | None = None,
    ) -> "KernelPlugin":
        """Create a plugin from an OpenAPI document."""

        if not openapi_document_path:
            raise PluginInitializationError("OpenAPI document path is required.")

        return cls(
            name=plugin_name,
            description=description,
            functions=OpenAPIPlugin.create(
                plugin_name=plugin_name,
                openapi_document_path=openapi_document_path,
                execution_settings=execution_settings,
            ),
        )

    @classmethod
    async def from_openai(
        cls,
        plugin_name: str,
        plugin_url: str | None = None,
        plugin_str: str | None = None,
        execution_parameters: OpenAIFunctionExecutionParameters | None = None,
        description: str | None = None,
    ) -> "KernelPlugin":
        """Create a plugin from the Open AI manifest.

        Args:
            plugin_name (str): The name of the plugin
            plugin_url (str | None): The URL of the plugin
            plugin_str (str | None): The JSON string of the plugin
            execution_parameters (OpenAIFunctionExecutionParameters | None): The execution parameters

        Returns:
            KernelPlugin: The imported plugin

        Raises:
            PluginInitializationError: if the plugin URL or plugin JSON/YAML is not provided
        """

        if execution_parameters is None:
            execution_parameters = OpenAIFunctionExecutionParameters()

        if plugin_str is not None:
            # Load plugin from the provided JSON string/YAML string
            openai_manifest = plugin_str
        elif plugin_url is not None:
            # Load plugin from the URL
            http_client = execution_parameters.http_client if execution_parameters.http_client else httpx.AsyncClient()
            openai_manifest = await DocumentLoader.from_uri(
                url=plugin_url, http_client=http_client, auth_callback=None, user_agent=execution_parameters.user_agent
            )
        else:
            raise PluginInitializationError("Either plugin_url or plugin_json must be provided.")

        try:
            plugin_json = json.loads(openai_manifest)
            openai_auth_config = OpenAIAuthenticationConfig(**plugin_json["auth"])
        except json.JSONDecodeError as ex:
            raise KernelPluginInvalidConfigurationError("Parsing of Open AI manifest for auth config failed.") from ex

        # Modify the auth callback in execution parameters if it's provided
        if execution_parameters and execution_parameters.auth_callback:
            initial_auth_callback = execution_parameters.auth_callback

            async def custom_auth_callback(**kwargs: Any):
                return await initial_auth_callback(plugin_name, openai_auth_config, **kwargs)

            execution_parameters.auth_callback = custom_auth_callback

        try:
            openapi_spec_url = OpenAIUtils.parse_openai_manifest_for_openapi_spec_url(plugin_json=plugin_json)
        except PluginInitializationError as ex:
            raise KernelPluginInvalidConfigurationError(
                "Parsing of Open AI manifest for OpenAPI spec URL failed."
            ) from ex
        return cls(
            name=plugin_name,
            description=description,
            functions=OpenAPIPlugin.create(
                plugin_name=plugin_name,
                openapi_document_path=openapi_spec_url,
                execution_settings=execution_parameters,
            ),
        )

    # endregion
    # region Internal Static Methods

    @staticmethod
    def _validate_functions(
        functions: (
            KERNEL_FUNCTION_TYPE
            | list[KERNEL_FUNCTION_TYPE | KernelPlugin]
            | dict[str, KERNEL_FUNCTION_TYPE]
            | KernelPlugin
            | None
        ),
        plugin_name: str,
    ) -> dict[str, KernelFunction]:
        """Validates the functions and returns a dictionary of functions."""
        if not functions or not plugin_name:
            # if the plugin_name is not present, the validation will fail, so no point in parsing.
            return {}
        if isinstance(functions, dict):
            return {
                name: KernelPlugin._parse_or_copy(function=function, plugin_name=plugin_name)
                for name, function in functions.items()
            }
        if isinstance(functions, KernelPlugin):
            return {
                name: function.function_copy(plugin_name=plugin_name) for name, function in functions.functions.items()
            }
        if isinstance(functions, KernelFunction):
            return {functions.name: KernelPlugin._parse_or_copy(function=functions, plugin_name=plugin_name)}
        if isinstance(functions, Callable):
            function = KernelPlugin._parse_or_copy(function=functions, plugin_name=plugin_name)
            return {function.name: function}
        if isinstance(functions, list):
            functions_dict: dict[str, KernelFunction] = {}
            for function in functions:
                if isinstance(function, (KernelFunction, Callable)):
                    function = KernelPlugin._parse_or_copy(function=function, plugin_name=plugin_name)
                    functions_dict[function.name] = function
                elif isinstance(function, KernelPlugin):  # type: ignore
                    functions_dict.update(
                        {
                            name: KernelPlugin._parse_or_copy(function=function, plugin_name=plugin_name)
                            for name, function in function.functions.items()
                        }
                    )
                else:
                    raise ValueError(f"Invalid type for functions in list: {function} (type: {type(function)})")
            return functions_dict
        raise ValueError(f"Invalid type for supplied functions: {functions} (type: {type(functions)})")

    @staticmethod
    def _parse_or_copy(function: KERNEL_FUNCTION_TYPE, plugin_name: str) -> KernelFunction:
        """Handle the function and return a KernelFunction instance."""
        if isinstance(function, KernelFunction):
            return function.function_copy(plugin_name=plugin_name)
        if isinstance(function, Callable):
            return KernelFunctionFromMethod(method=function, plugin_name=plugin_name)
        raise ValueError(f"Invalid type for function: {function} (type: {type(function)})")

    # endregion

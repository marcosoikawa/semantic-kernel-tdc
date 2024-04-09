# Copyright (c) Microsoft. All rights reserved.
from __future__ import annotations

import logging
from copy import copy
from typing import TYPE_CHECKING, Any, AsyncIterable, Callable, Literal, Type, TypeVar, Union

from pydantic import Field, field_validator

from semantic_kernel.connectors.ai.prompt_execution_settings import PromptExecutionSettings
from semantic_kernel.contents.streaming_content_mixin import StreamingContentMixin
from semantic_kernel.events import FunctionInvokedEventArgs, FunctionInvokingEventArgs
from semantic_kernel.exceptions import (
    KernelFunctionAlreadyExistsError,
    KernelFunctionNotFoundError,
    KernelInvokeException,
    KernelPluginNotFoundError,
    KernelServiceNotFoundError,
    ServiceInvalidTypeError,
    TemplateSyntaxError,
)
from semantic_kernel.functions.function_result import FunctionResult
from semantic_kernel.functions.kernel_arguments import KernelArguments
from semantic_kernel.functions.kernel_function_metadata import KernelFunctionMetadata
from semantic_kernel.functions.kernel_plugin import KernelPlugin
from semantic_kernel.kernel_pydantic import KernelBaseModel
from semantic_kernel.prompt_template.const import KERNEL_TEMPLATE_FORMAT_NAME
from semantic_kernel.reliability.pass_through_without_retry import PassThroughWithoutRetry
from semantic_kernel.reliability.retry_mechanism_base import RetryMechanismBase
from semantic_kernel.services.ai_service_client_base import AIServiceClientBase
from semantic_kernel.services.ai_service_selector import AIServiceSelector

if TYPE_CHECKING:
    from semantic_kernel.connectors.ai.chat_completion_client_base import ChatCompletionClientBase
    from semantic_kernel.connectors.ai.embeddings.embedding_generator_base import EmbeddingGeneratorBase
    from semantic_kernel.connectors.ai.text_completion_client_base import TextCompletionClientBase
    from semantic_kernel.functions.kernel_function import KernelFunction

T = TypeVar("T")

ALL_SERVICE_TYPES = Union["TextCompletionClientBase", "ChatCompletionClientBase", "EmbeddingGeneratorBase"]

logger: logging.Logger = logging.getLogger(__name__)


class Kernel(KernelBaseModel):
    """
    The Kernel class is the main entry point for the Semantic Kernel. It provides the ability to run
    semantic/native functions, and manage plugins, memory, and AI services.

    Attributes:
        plugins (KernelPluginCollection | None): The collection of plugins to be used by the kernel
        services (dict[str, AIServiceClientBase]): The services to be used by the kernel
        retry_mechanism (RetryMechanismBase): The retry mechanism to be used by the kernel
        function_invoking_handlers (dict): The function invoking handlers
        function_invoked_handlers (dict): The function invoked handlers
    """

    # region Init

    plugins: dict[str, KernelPlugin] = Field(default_factory=dict)
    services: dict[str, AIServiceClientBase] = Field(default_factory=dict)
    ai_service_selector: AIServiceSelector = Field(default_factory=AIServiceSelector)
    retry_mechanism: RetryMechanismBase = Field(default_factory=PassThroughWithoutRetry)
    function_invoking_handlers: dict[
        int, Callable[["Kernel", FunctionInvokingEventArgs], FunctionInvokingEventArgs]
    ] = Field(default_factory=dict)
    function_invoked_handlers: dict[int, Callable[["Kernel", FunctionInvokedEventArgs], FunctionInvokedEventArgs]] = (
        Field(default_factory=dict)
    )

    def __init__(
        self,
        plugins: dict[str, KernelPlugin] | None = None,
        services: AIServiceClientBase | list[AIServiceClientBase] | dict[str, AIServiceClientBase] | None = None,
        ai_service_selector: AIServiceSelector | None = None,
        **kwargs: Any,
    ) -> None:
        """
        Initialize a new instance of the Kernel class.

        Args:
            plugins (KernelPluginCollection | None): The collection of plugins to be used by the kernel
            services (AIServiceClientBase | list[AIServiceClientBase] | dict[str, AIServiceClientBase] | None:
                The services to be used by the kernel,
                will be rewritten to a dict with service_id as key
            ai_service_selector (AIServiceSelector | None): The AI service selector to be used by the kernel,
                default is based on order of execution settings.
            **kwargs (Any): Additional fields to be passed to the Kernel model,
                these are limited to retry_mechanism and function_invoking_handlers
                and function_invoked_handlers, the best way to add function_invoking_handlers
                and function_invoked_handlers is to use the add_function_invoking_handler
                and add_function_invoked_handler methods.
        """
        args = {
            "services": services,
            **kwargs,
        }
        if ai_service_selector:
            args["ai_service_selector"] = ai_service_selector
        if plugins:
            args["plugins"] = plugins
        super().__init__(**args)

    @field_validator("services", mode="before")
    @classmethod
    def rewrite_services(
        cls,
        services: AIServiceClientBase | list[AIServiceClientBase] | dict[str, AIServiceClientBase] | None = None,
    ) -> dict[str, AIServiceClientBase]:
        """Rewrite services to a dictionary."""
        if not services:
            return {}
        if isinstance(services, AIServiceClientBase):
            return {services.service_id or "default": services}
        if isinstance(services, list):
            return {s.service_id or "default": s for s in services}
        return services

    # endregion
    # region Invoke Functions

    async def invoke_stream(
        self,
        function: "KernelFunction" | None = None,
        arguments: KernelArguments | None = None,
        function_name: str | None = None,
        plugin_name: str | None = None,
        return_function_results: bool | None = False,
        **kwargs: Any,
    ) -> AsyncIterable[list["StreamingContentMixin"] | FunctionResult | list[FunctionResult]]:
        """Execute one or more stream functions.

        This will execute the functions in the order they are provided, if a list of functions is provided.
        When multiple functions are provided only the last one is streamed, the rest is executed as a pipeline.

        Arguments:
            functions (KernelFunction): The function or functions to execute,
            this value has precedence when supplying both this and using function_name and plugin_name,
            if this is none, function_name and plugin_name are used and cannot be None.
            arguments (KernelArguments): The arguments to pass to the function(s), optional
            function_name (str | None): The name of the function to execute
            plugin_name (str | None): The name of the plugin to execute
            return_function_results (bool | None): If True, the function results are returned in addition to
                the streaming content, otherwise only the streaming content is returned.
            kwargs (dict[str, Any]): arguments that can be used instead of supplying KernelArguments

        Yields:
            StreamingContentMixin: The content of the stream of the last function provided.
        """
        if arguments is None:
            arguments = KernelArguments(**kwargs)
        if not function:
            if not function_name or not plugin_name:
                raise KernelFunctionNotFoundError("No function(s) or function- and plugin-name provided")
            function = self.func(plugin_name, function_name)

        function_invoking_args = self.on_function_invoking(function.metadata, arguments)
        if function_invoking_args.is_cancel_requested:
            logger.info(
                f"Execution was cancelled on function invoking event of function: {function.fully_qualified_name}."
            )
            return
        if function_invoking_args.updated_arguments:
            logger.info(
                "Arguments updated by function_invoking_handler in function, "
                f"new arguments: {function_invoking_args.arguments}"
            )
            arguments = function_invoking_args.arguments
        if function_invoking_args.is_skip_requested:
            logger.info(
                f"Execution was skipped on function invoking event of function: {function.fully_qualified_name}."
            )
            return
        function_result: list[list["StreamingContentMixin"] | Any] = []

        async for stream_message in function.invoke_stream(self, arguments):
            if isinstance(stream_message, FunctionResult) and (
                exception := stream_message.metadata.get("exception", None)
            ):
                raise KernelInvokeException(
                    f"Error occurred while invoking function: '{function.fully_qualified_name}'"
                ) from exception
            function_result.append(stream_message)
            yield stream_message

        if return_function_results:
            output_function_result: list["StreamingContentMixin"] = []
            for result in function_result:
                for choice in result:
                    if not isinstance(choice, StreamingContentMixin):
                        continue
                    if len(output_function_result) <= choice.choice_index:
                        output_function_result.append(copy(choice))
                    else:
                        output_function_result[choice.choice_index] += choice
            yield FunctionResult(function=function.metadata, value=output_function_result)

    async def invoke(
        self,
        function: "KernelFunction" | None = None,
        arguments: KernelArguments | None = None,
        function_name: str | None = None,
        plugin_name: str | None = None,
        **kwargs: Any,
    ) -> FunctionResult | None:
        """Execute one or more functions.

        When multiple functions are passed the FunctionResult of each is put into a list.

        Arguments:
            function (KernelFunction): The function or functions to execute,
            this value has precedence when supplying both this and using function_name and plugin_name,
            if this is none, function_name and plugin_name are used and cannot be None.
            arguments (KernelArguments): The arguments to pass to the function(s), optional
            function_name (str | None): The name of the function to execute
            plugin_name (str | None): The name of the plugin to execute
            kwargs (dict[str, Any]): arguments that can be used instead of supplying KernelArguments

        Returns:
            FunctionResult | list[FunctionResult] | None: The result of the function(s)

        """
        if arguments is None:
            arguments = KernelArguments(**kwargs)
        if not function:
            if not function_name or not plugin_name:
                raise KernelFunctionNotFoundError("No function or plugin name provided")
            function = self.func(plugin_name, function_name)
        function_invoking_args = self.on_function_invoking(function.metadata, arguments)
        if function_invoking_args.is_cancel_requested:
            logger.info(
                f"Execution was cancelled on function invoking event of function: {function.fully_qualified_name}."
            )
            return None
        if function_invoking_args.updated_arguments:
            logger.info(
                f"Arguments updated by function_invoking_handler, new arguments: {function_invoking_args.arguments}"
            )
            arguments = function_invoking_args.arguments
        function_result = None
        exception = None
        try:
            function_result = await function.invoke(self, arguments)
        except Exception as exc:
            logger.error(
                "Something went wrong in function invocation. During function invocation:"
                f" '{function.fully_qualified_name}'. Error description: '{str(exc)}'"
            )
            exception = exc

        # this allows a hook to alter the results before adding.
        function_invoked_args = self.on_function_invoked(function.metadata, arguments, function_result, exception)
        if function_invoked_args.exception:
            raise KernelInvokeException(
                f"Error occurred while invoking function: '{function.fully_qualified_name}'"
            ) from function_invoked_args.exception
        if function_invoked_args.is_cancel_requested:
            logger.info(
                f"Execution was cancelled on function invoked event of function: {function.fully_qualified_name}."
            )
            return (
                function_invoked_args.function_result
                if function_invoked_args.function_result
                else FunctionResult(function=function.metadata, value=None, metadata={})
            )
        if function_invoked_args.updated_arguments:
            logger.info(
                f"Arguments updated by function_invoked_handler in function {function.fully_qualified_name}"
                ", new arguments: {function_invoked_args.arguments}"
            )
            arguments = function_invoked_args.arguments
        if function_invoked_args.is_repeat_requested:
            logger.info(
                f"Execution was repeated on function invoked event of function: {function.fully_qualified_name}."
            )
            return await self.invoke(function=function, arguments=arguments)

        return (
            function_invoked_args.function_result
            if function_invoked_args.function_result
            else FunctionResult(function=function.metadata, value=None, metadata={})
        )

    async def invoke_prompt(
        self,
        function_name: str,
        plugin_name: str,
        prompt: str,
        arguments: KernelArguments | None = None,
        template_format: Literal[
            "semantic-kernel",
            "handlebars",
            "jinja2",
        ] = KERNEL_TEMPLATE_FORMAT_NAME,
        **kwargs: Any,
    ) -> FunctionResult | None:
        """
        Invoke a function from the provided prompt

        Args:
            function_name (str): The name of the function
            plugin_name (str): The name of the plugin
            prompt (str): The prompt to use
            arguments (KernelArguments | None): The arguments to pass to the function(s), optional
            template_format (str | None): The format of the prompt template
            kwargs (dict[str, Any]): arguments that can be used instead of supplying KernelArguments

        Returns:
            FunctionResult | list[FunctionResult] | None: The result of the function(s)
        """
        if not arguments:
            arguments = KernelArguments(**kwargs)
        if not prompt:
            raise TemplateSyntaxError("The prompt is either null or empty.")

        from semantic_kernel.functions.kernel_function_from_prompt import KernelFunctionFromPrompt

        function = KernelFunctionFromPrompt(
            function_name=function_name,
            plugin_name=plugin_name,
            prompt=prompt,
            template_format=template_format,
        )
        return await self.invoke(function=function, arguments=arguments)

    # endregion
    # region Function Invoking/Invoked Events

    def on_function_invoked(
        self,
        kernel_function_metadata: KernelFunctionMetadata,
        arguments: KernelArguments,
        function_result: FunctionResult | None = None,
        exception: Exception | None = None,
    ) -> FunctionInvokedEventArgs:
        # TODO: include logic that uses function_result
        args = FunctionInvokedEventArgs(
            kernel_function_metadata=kernel_function_metadata,
            arguments=arguments,
            function_result=function_result,
            exception=exception or function_result.metadata.get("exception", None) if function_result else None,
        )
        if self.function_invoked_handlers:
            for handler in self.function_invoked_handlers.values():
                handler(self, args)
        return args

    def on_function_invoking(
        self, kernel_function_metadata: KernelFunctionMetadata, arguments: KernelArguments
    ) -> FunctionInvokingEventArgs:
        args = FunctionInvokingEventArgs(kernel_function_metadata=kernel_function_metadata, arguments=arguments)
        if self.function_invoking_handlers:
            for handler in self.function_invoking_handlers.values():
                handler(self, args)
        return args

    def add_function_invoking_handler(
        self, handler: Callable[["Kernel", FunctionInvokingEventArgs], FunctionInvokingEventArgs]
    ) -> None:
        self.function_invoking_handlers[id(handler)] = handler

    def add_function_invoked_handler(
        self, handler: Callable[["Kernel", FunctionInvokedEventArgs], FunctionInvokedEventArgs]
    ) -> None:
        self.function_invoked_handlers[id(handler)] = handler

    def remove_function_invoking_handler(self, handler: Callable) -> None:
        if id(handler) in self.function_invoking_handlers:
            del self.function_invoking_handlers[id(handler)]

    def remove_function_invoked_handler(self, handler: Callable) -> None:
        if id(handler) in self.function_invoked_handlers:
            del self.function_invoked_handlers[id(handler)]

    # endregion
    # region Plugins & Functions

    def add_plugin(self, plugin: KernelPlugin) -> None:
        """
        Adds a plugin to the kernel's collection of plugins. If a plugin instance is provided,
        it uses that instance instead of creating a new KernelPlugin.

        Args:
            plugin (KernelPlugin): The plugin to add.
        """
        self.plugins[plugin.name] = plugin

    def add_plugins(self, plugins: list[KernelPlugin] | dict[str, KernelPlugin]) -> None:
        """
        Adds a list of plugins to the kernel's collection of plugins.

        Args:
            plugins (list[KernelPlugin] | dict[str, KernelPlugin]): The plugins to add to the kernel
        """
        if isinstance(plugins, list):
            plugins = {plugin.name: plugin for plugin in plugins}
        self.plugins.update(plugins)

    def add_function(self, plugin_name: str, function: "KernelFunction" | Callable[..., Any]) -> None:
        """
        Adds a function to the specified plugin.

        Args:
            plugin_name (str): The name of the plugin to add the function to
            function (KernelFunction): The function to add
        """
        return self.add_functions(plugin_name, [function])

    def add_functions(
        self,
        plugin_name: str,
        functions: list["KernelFunction" | Callable[..., Any]] | dict[str, "KernelFunction" | Callable[..., Any]],
    ) -> None:
        """
        Adds a list of functions to the specified plugin.

        Args:
            plugin_name (str): The name of the plugin to add the functions to
            functions (list[KernelFunction] | dict[str, KernelFunction]): The functions to add
        """
        if plugin_name in self.plugins:
            self.plugins[plugin_name].update(functions)
            return
        self.add_plugins({plugin_name: KernelPlugin(name=plugin_name, functions=functions)})  # type: ignore

    def add_plugin_from_object(
        self, plugin_name: str, plugin_instance: Any | dict[str, Any], description: str | None = None
    ) -> None:
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
        plugin = KernelPlugin.from_object(
            plugin_name=plugin_name, plugin_instance=plugin_instance, description=description
        )
        self.add_plugin(plugin)

    def add_plugin_from_directory(self, plugin_name: str, parent_directory: str, description: str | None = None):
        """Create a plugin from a specified directory and add it to the kernel.

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

            Calling `add_plugin_from_directory("MyPlugins", "/path/to")` will create a KernelPlugin object named
                "MyPlugins", containing KernelFunction objects for `pluginA.yaml`, `pluginB.yaml`,
                `Directory1`, and `Directory2`, each initialized with their respective configurations.
                And functions for anything within native_function.py.

        Args:
            plugin_name (str): The name of the plugin, this is the name of the directory within the parent directory
            parent_directory (str): The parent directory path where the plugin directory resides
            description (str | None): The description of the plugin

        Raises:
            PluginInitializationError: If the plugin directory does not exist.
            PluginInvalidNameError: If the plugin name is invalid.
        """
        plugin = KernelPlugin.from_directory(
            plugin_name=plugin_name, parent_directory=parent_directory, description=description
        )
        self.add_plugin(plugin)

    def func(self, plugin_name: str, function_name: str) -> "KernelFunction":
        if plugin_name not in self.plugins:
            raise KernelPluginNotFoundError(f"Plugin '{plugin_name}' not found")
        if function_name not in self.plugins[plugin_name]:
            raise KernelFunctionNotFoundError(f"Function '{function_name}' not found in plugin '{plugin_name}'")
        return self.plugins[plugin_name][function_name]

    def func_from_fully_qualified_function_name(self, fully_qualified_function_name: str) -> "KernelFunction":
        plugin_name, function_name = fully_qualified_function_name.split("-")
        if plugin_name not in self.plugins:
            raise KernelPluginNotFoundError(f"Plugin '{plugin_name}' not found")
        if function_name not in self.plugins[plugin_name]:
            raise KernelFunctionNotFoundError(f"Function '{function_name}' not found in plugin '{plugin_name}'")
        return self.plugins[plugin_name][function_name]

    # endregion
    # region Services

    def select_ai_service(
        self, function: "KernelFunction", arguments: KernelArguments
    ) -> tuple[ALL_SERVICE_TYPES, PromptExecutionSettings]:
        """Uses the AI service selector to select a service for the function."""
        return self.ai_service_selector.select_ai_service(self, function, arguments)

    def get_service(
        self,
        service_id: str | None = None,
        type: Type[ALL_SERVICE_TYPES] | None = None,
    ) -> "AIServiceClientBase":
        """Get a service by service_id and type.

        Type is optional and when not supplied, no checks are done.
        Type should be
            TextCompletionClientBase, ChatCompletionClientBase, EmbeddingGeneratorBase
            or a subclass of one.
            You can also check for multiple types in one go,
            by using TextCompletionClientBase | ChatCompletionClientBase.

        If type and service_id are both None, the first service is returned.

        Args:
            service_id (str | None): The service id,
                if None, the default service is returned or the first service is returned.
            type (Type[ALL_SERVICE_TYPES] | None): The type of the service, if None, no checks are done.

        Returns:
            ALL_SERVICE_TYPES: The service.

        Raises:
            ValueError: If no service is found that matches the type.

        """
        service: "AIServiceClientBase | None" = None
        if not service_id or service_id == "default":
            if not type:
                if default_service := self.services.get("default"):
                    return default_service
                return list(self.services.values())[0]
            if default_service := self.services.get("default"):
                if isinstance(default_service, type):
                    return default_service
            for service in self.services.values():
                if isinstance(service, type):
                    return service
            raise KernelServiceNotFoundError(f"No service found of type {type}")
        if not (service := self.services.get(service_id)):
            raise KernelServiceNotFoundError(f"Service with service_id '{service_id}' does not exist")
        if type and not isinstance(service, type):
            raise ServiceInvalidTypeError(f"Service with service_id '{service_id}' is not of type {type}")
        return service

    def get_services_by_type(self, type: Type[ALL_SERVICE_TYPES]) -> dict[str, "AIServiceClientBase"]:
        return {service.service_id: service for service in self.services.values() if isinstance(service, type)}

    def get_prompt_execution_settings_from_service_id(
        self, service_id: str, type: Type[ALL_SERVICE_TYPES] | None = None
    ) -> PromptExecutionSettings:
        """Get the specific request settings from the service, instantiated with the service_id and ai_model_id."""
        service = self.get_service(service_id, type=type)
        return service.instantiate_prompt_execution_settings(
            service_id=service_id,
            extension_data={"ai_model_id": service.ai_model_id},
        )

    def add_service(self, service: AIServiceClientBase, overwrite: bool = False) -> None:
        if service.service_id not in self.services or overwrite:
            self.services[service.service_id] = service
        else:
            raise KernelFunctionAlreadyExistsError(f"Service with service_id '{service.service_id}' already exists")

    def remove_service(self, service_id: str) -> None:
        """Delete a single service from the Kernel."""
        if service_id not in self.services:
            raise KernelServiceNotFoundError(f"Service with service_id '{service_id}' does not exist")
        del self.services[service_id]

    def remove_all_services(self) -> None:
        """Removes the services from the Kernel, does not delete them."""
        self.services.clear()

    # endregion

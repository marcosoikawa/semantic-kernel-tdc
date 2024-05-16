# Copyright (c) Microsoft. All rights reserved.
from __future__ import annotations

import os
import warnings
from typing import TYPE_CHECKING, Callable

import pytest

from semantic_kernel.contents.chat_history import ChatHistory
from semantic_kernel.contents.streaming_text_content import StreamingTextContent
from semantic_kernel.filters.function.function_invocation_context import FunctionInvocationContext
from semantic_kernel.functions.function_result import FunctionResult
from semantic_kernel.functions.kernel_function import KernelFunction
from semantic_kernel.functions.kernel_function_decorator import kernel_function
from semantic_kernel.functions.kernel_function_metadata import KernelFunctionMetadata
from semantic_kernel.kernel import Kernel
from semantic_kernel.services.ai_service_client_base import AIServiceClientBase
from semantic_kernel.utils.settings import (
    azure_openai_settings_from_dot_env,
    google_palm_settings_from_dot_env,
    openai_settings_from_dot_env,
)

if TYPE_CHECKING:
    from semantic_kernel.kernel import Kernel
    from semantic_kernel.services.ai_service_client_base import AIServiceClientBase


@pytest.fixture(scope="function")
def kernel() -> "Kernel":
    from semantic_kernel.kernel import Kernel

    return Kernel()


@pytest.fixture(scope="session")
def service() -> "AIServiceClientBase":
    from semantic_kernel.services.ai_service_client_base import AIServiceClientBase

    return AIServiceClientBase(service_id="service", ai_model_id="ai_model_id")


@pytest.fixture(scope="session")
def default_service() -> "AIServiceClientBase":
    from semantic_kernel.services.ai_service_client_base import AIServiceClientBase

    return AIServiceClientBase(service_id="default", ai_model_id="ai_model_id")


@pytest.fixture(scope="function")
def kernel_with_service(kernel: "Kernel", service: "AIServiceClientBase") -> "Kernel":
    kernel.add_service(service)
    return kernel


@pytest.fixture(scope="function")
def kernel_with_default_service(kernel: "Kernel", default_service: "AIServiceClientBase") -> "Kernel":
    kernel.add_service(default_service)
    return kernel


@pytest.fixture(scope="session")
def not_decorated_native_function() -> Callable:
    def not_decorated_native_function(arg1: str) -> str:
        return "test"

    return not_decorated_native_function


@pytest.fixture(scope="session")
def decorated_native_function() -> Callable:
    @kernel_function(name="getLightStatus")
    def decorated_native_function(arg1: str) -> str:
        return "test"

    return decorated_native_function


@pytest.fixture(scope="session")
def custom_plugin_class():
    class CustomPlugin:
        @kernel_function(name="getLightStatus")
        def decorated_native_function(self) -> str:
            return "test"

    return CustomPlugin


@pytest.fixture(scope="session")
def create_mock_function() -> Callable:
    from semantic_kernel.functions.kernel_function import KernelFunction

    async def stream_func(*args, **kwargs):
        yield [StreamingTextContent(choice_index=0, text="test", metadata={})]

    def create_mock_function(name: str, value: str = "test") -> "KernelFunction":
        kernel_function_metadata = KernelFunctionMetadata(
            name=name,
            plugin_name="TestPlugin",
            description="test description",
            parameters=[],
            is_prompt=True,
            is_asynchronous=True,
        )

        class CustomKernelFunction(KernelFunction):
            call_count: int = 0

            async def _invoke_internal_stream(
                self,
                context: "FunctionInvocationContext",
            ) -> None:
                self.call_count += 1
                context.result = FunctionResult(
                    function=kernel_function_metadata,
                    value=stream_func(),
                )

            async def _invoke_internal(self, context: "FunctionInvocationContext"):
                self.call_count += 1
                context.result = FunctionResult(function=kernel_function_metadata, value=value, metadata={})

        mock_function = CustomKernelFunction(metadata=kernel_function_metadata)

        return mock_function

    return create_mock_function


@pytest.fixture(scope="function")
def chat_history():
    return ChatHistory()


@pytest.fixture(autouse=True)
def enable_debug_mode():
    """Set `autouse=True` to enable easy debugging for tests.

    How to debug:
    1. Ensure [snoop](https://github.com/alexmojaki/snoop) is installed
        (`pip install snoop`).
    2. If you're doing print based debugging, use `pr` instead of `print`.
        That is, convert `print(some_var)` to `pr(some_var)`.
    3. If you want a trace of a particular functions calls, just add `ss()` as the first
        line of the function.

    NOTE:
    ----
        It's completely fine to leave `autouse=True` in the fixture. It doesn't affect
        the tests unless you use `pr` or `ss` in any test.

    NOTE:
    ----
        When you use `ss` or `pr` in a test, pylance or mypy will complain. This is
        because they don't know that we're adding these functions to the builtins. The
        tests will run fine though.
    """
    import builtins

    try:
        import snoop
    except ImportError:
        warnings.warn(
            "Install snoop to enable trace debugging. `pip install snoop`",
            ImportWarning,
        )
        return

    builtins.ss = snoop.snoop(depth=4).__enter__
    builtins.pr = snoop.pp


@pytest.fixture(scope="session")
def get_aoai_config():
    if "Python_Integration_Tests" in os.environ:
        deployment_name = os.environ["AzureOpenAIEmbeddings__DeploymentName"]
        api_key = os.environ["AzureOpenAI_EastUS__ApiKey"]
        endpoint = os.environ["AzureOpenAI_EastUS__Endpoint"]
    else:
        # Load credentials from .env file
        deployment_name, api_key, endpoint = azure_openai_settings_from_dot_env()
        deployment_name = "text-embedding-ada-002"

    return deployment_name, api_key, endpoint


@pytest.fixture(scope="session")
def get_oai_config():
    if "Python_Integration_Tests" in os.environ:
        api_key = os.environ["OpenAI__ApiKey"]
        org_id = None
    else:
        # Load credentials from .env file
        api_key, org_id = openai_settings_from_dot_env()

    return api_key, org_id


@pytest.fixture(scope="session")
def get_gp_config():
    if "Python_Integration_Tests" in os.environ:
        api_key = os.environ["GOOGLE_PALM_API_KEY"]
    else:
        # Load credentials from .env file
        api_key = google_palm_settings_from_dot_env()

    return api_key

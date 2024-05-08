# Copyright (c) Microsoft. All rights reserved.

import asyncio
import datetime

from azure.core.credentials import AccessToken
from azure.core.exceptions import ClientAuthenticationError
from azure.identity import DefaultAzureCredential

from semantic_kernel.connectors.ai.open_ai.prompt_execution_settings.azure_chat_prompt_execution_settings import (
    AzureChatPromptExecutionSettings,
)
from semantic_kernel.connectors.ai.open_ai.services.azure_chat_completion import AzureChatCompletion
from semantic_kernel.connectors.ai.function_call_behavior import FunctionCallBehavior
from semantic_kernel.contents.chat_history import ChatHistory
from semantic_kernel.core_plugins.sessions_python_tool.sessions_python_plugin import (
    SessionsPythonTool,
)
from semantic_kernel.core_plugins.time_plugin import TimePlugin
from semantic_kernel.exceptions.function_exceptions import FunctionExecutionException
from semantic_kernel.functions.kernel_arguments import KernelArguments
from semantic_kernel.kernel import Kernel
from semantic_kernel.utils.settings import (
    azure_container_apps_settings_from_dot_env_as_dict,
    azure_openai_settings_from_dot_env_as_dict,
)

auth_token: AccessToken | None = None

ACA_TOKEN_ENDPOINT = "https://acasessions.io/.default"


async def auth_callback() -> str:
    """Auth callback for the SessionsPythonTool.
    This is a sample auth callback that shows how to use Azure's DefaultAzureCredential
    to get an access token.
    """
    global auth_token
    current_utc_timestamp = int(datetime.datetime.now(datetime.timezone.utc).timestamp())

    if not auth_token or auth_token.expires_on < current_utc_timestamp:
        credential = DefaultAzureCredential()

        try:
            auth_token = credential.get_token(ACA_TOKEN_ENDPOINT)
        except ClientAuthenticationError as cae:
            err_messages = getattr(cae, "messages", [])
            raise FunctionExecutionException(
                f"Failed to retrieve the client auth token with messages: {' '.join(err_messages)}"
            ) from cae

    return auth_token.token


kernel = Kernel()

service_id = "sessions-tool"
chat_service = AzureChatCompletion(
    service_id=service_id, **azure_openai_settings_from_dot_env_as_dict(include_api_version=True)
)
kernel.add_service(chat_service)

sessions_tool = SessionsPythonTool(
    **azure_container_apps_settings_from_dot_env_as_dict(),
    auth_callback=auth_callback,
)

kernel.add_plugin(sessions_tool, "SessionsTool")
kernel.add_plugin(TimePlugin(), "Time")

chat_function = kernel.add_function(
    prompt="{{$chat_history}}{{$user_input}}",
    plugin_name="ChatBot",
    function_name="Chat",
)

req_settings = AzureChatPromptExecutionSettings(service_id=service_id, tool_choice="auto")

filter = {"excluded_plugins": ["ChatBot"]}
req_settings.function_call_behavior = FunctionCallBehavior.EnableFunctions(auto_invoke=True, filters=filter)

arguments = KernelArguments(settings=req_settings)

history = ChatHistory()


async def chat() -> bool:
    try:
        user_input = input("User:> ")
    except KeyboardInterrupt:
        print("\n\nExiting chat...")
        return False
    except EOFError:
        print("\n\nExiting chat...")
        return False

    if user_input == "exit":
        print("\n\nExiting chat...")
        return False

    arguments["chat_history"] = history
    arguments["user_input"] = user_input
    answer = await kernel.invoke(
        function=chat_function,
        arguments=arguments,
    )
    print(f"Mosscap:> {answer}")
    history.add_user_message(user_input)
    history.add_assistant_message(str(answer))
    return True


async def main() -> None:
    print(
        "Welcome to the chat bot!\
        \n  Type 'exit' to exit.\
        \n  Try a Python code execution question to see the function calling in action (i.e. what is 1+1?)."
    )
    chatting = True
    while chatting:
        chatting = await chat()


if __name__ == "__main__":
    asyncio.run(main())

# Copyright (c) Microsoft. All rights reserved.
from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from mistralai.async_client import MistralAsyncClient

from semantic_kernel.connectors.ai.chat_completion_client_base import ChatCompletionClientBase
from semantic_kernel.connectors.ai.function_choice_behavior import FunctionChoiceBehavior, FunctionChoiceType
from semantic_kernel.connectors.ai.mistral_ai.prompt_execution_settings.mistral_ai_prompt_execution_settings import (
    MistralAIChatPromptExecutionSettings,
)
from semantic_kernel.connectors.ai.mistral_ai.services.mistral_ai_chat_completion import MistralAIChatCompletion
from semantic_kernel.connectors.ai.open_ai.prompt_execution_settings.open_ai_prompt_execution_settings import (
    OpenAIChatPromptExecutionSettings,
)
from semantic_kernel.contents.chat_history import ChatHistory
from semantic_kernel.contents.chat_message_content import (
    ChatMessageContent,
    FunctionCallContent,
    FunctionResultContent,
    TextContent,
)
from semantic_kernel.contents.utils.author_role import AuthorRole
from semantic_kernel.exceptions import (
    ServiceInitializationError,
    ServiceInvalidExecutionSettingsError,
    ServiceResponseException,
)
from semantic_kernel.functions.kernel_arguments import KernelArguments
from semantic_kernel.kernel import Kernel


@pytest.fixture
def mock_settings() -> MistralAIChatPromptExecutionSettings:
    return MistralAIChatPromptExecutionSettings()


@pytest.fixture
def mock_mistral_ai_client_completion() -> MistralAsyncClient:
    client = MagicMock(spec=MistralAsyncClient)
    chat_completion_response = AsyncMock()
    choices = [MagicMock(finish_reason="stop", message=MagicMock(role="assistant", content="Test"))]
    chat_completion_response.choices = choices
    client.chat.return_value = chat_completion_response
    return client


@pytest.fixture
def mock_mistral_ai_client_completion_stream() -> MistralAsyncClient:
    client = MagicMock(spec=MistralAsyncClient)
    chat_completion_response = MagicMock()
    choices = [
        MagicMock(finish_reason="stop", delta=MagicMock(role="assistant", content="Test")),
        MagicMock(finish_reason="stop", delta=MagicMock(role="assistant", content="Test", tool_calls=None)),
    ]
    chat_completion_response.choices = choices
    chat_completion_response_empty = MagicMock()
    chat_completion_response_empty.choices = []
    generator_mock = MagicMock()
    generator_mock.__aiter__.return_value = [chat_completion_response_empty, chat_completion_response]
    client.chat_stream.return_value = generator_mock
    return client


@pytest.mark.asyncio
async def test_complete_chat_contents(
    kernel: Kernel,
    mock_settings: MistralAIChatPromptExecutionSettings,
    mock_mistral_ai_client_completion: MistralAsyncClient,
):
    chat_history = MagicMock()
    arguments = KernelArguments()
    chat_completion_base = MistralAIChatCompletion(
        ai_model_id="test_model_id", service_id="test", api_key="", async_client=mock_mistral_ai_client_completion
    )

    content: list[ChatMessageContent] = await chat_completion_base.get_chat_message_contents(
        chat_history=chat_history, settings=mock_settings, kernel=kernel, arguments=arguments
    )
    assert content is not None
    

@pytest.mark.asyncio
async def test_complete_chat_stream_contents(
    kernel: Kernel,
    mock_settings: MistralAIChatPromptExecutionSettings,
    mock_mistral_ai_client_completion_stream: MistralAsyncClient,
):
    chat_history = MagicMock()
    arguments = KernelArguments()

    chat_completion_base = MistralAIChatCompletion(
        ai_model_id="test_model_id",
        service_id="test",
        api_key="",
        async_client=mock_mistral_ai_client_completion_stream,
    )

    async for content in chat_completion_base.get_streaming_chat_message_contents(
        chat_history, mock_settings, kernel=kernel, arguments=arguments
    ):
        assert content is not None


@pytest.mark.asyncio
async def test_mistral_ai_sdk_exception(kernel: Kernel, mock_settings: MistralAIChatPromptExecutionSettings):
    chat_history = MagicMock()
    arguments = KernelArguments()
    client = MagicMock(spec=MistralAsyncClient)
    client.chat.side_effect = Exception("Test Exception")

    chat_completion_base = MistralAIChatCompletion(
        ai_model_id="test_model_id", service_id="test", api_key="", async_client=client
    )

    with pytest.raises(ServiceResponseException):
        await chat_completion_base.get_chat_message_contents(
            chat_history=chat_history, settings=mock_settings, kernel=kernel, arguments=arguments
        )
        

@pytest.mark.parametrize("function_choice_behavior", [
    pytest.param(
        FunctionChoiceBehavior.Auto(),
        id="auto"
    ),
    pytest.param(
        FunctionChoiceBehavior.Auto(auto_invoke=False),
        id="auto_none_invoke"
    ),
    pytest.param(
        FunctionChoiceBehavior.Required(auto_invoke=False),
        id="required"
    ),
    pytest.param(
        FunctionChoiceBehavior.NoneInvoke(),
        id="none"
    ),
])
@pytest.mark.asyncio
async def test_complete_chat_contents_function_call_behavior_tool_call(
    kernel: Kernel,
    mock_settings: MistralAIChatPromptExecutionSettings,
    function_choice_behavior: FunctionChoiceBehavior
):
    mock_settings.function_choice_behavior = function_choice_behavior
    
    # Prepare Connector Mocks
    mock_text = MagicMock(spec=TextContent)
    mock_message_text_content = ChatMessageContent(
        role=AuthorRole.ASSISTANT, items=[mock_text]
    )
    
    mock_function_call = MagicMock(spec=FunctionCallContent)
    mock_message_function_call = ChatMessageContent(
        role=AuthorRole.ASSISTANT, items=[mock_function_call]
    )
    
    mock_function_result = MagicMock(
            spec=FunctionResultContent,
            result="Test",
            id="test_id"
        )
    mock_message_function_result = ChatMessageContent(
        role=AuthorRole.TOOL, items=[mock_function_result]
    )
    
    if function_choice_behavior.type_ is not FunctionChoiceType.NONE:
        # Mock Tool Flow FunctionCall --> FunctionResult --> TextContent
        mock_messages = [[mock_message_function_call], [mock_message_text_content]]
    else:
        # Mock None Flow TextContent
        mock_messages = [[mock_message_text_content]]

    arguments = KernelArguments()
    chat_completion_base = MistralAIChatCompletion(
        ai_model_id="test_model_id", service_id="test", api_key=""
    )

    def fake_function_result(
            function_call,
            chat_history: ChatHistory,
            arguments,
            function_call_count,
            request_index,
            function_behavior,
    ):
        chat_history.add_message(message=mock_message_function_result)
        return

    with (
        patch.object(
            chat_completion_base,
            '_send_chat_request',
              side_effect=mock_messages),
        patch(
            "semantic_kernel.kernel.Kernel.invoke_function_call",
            side_effect=fake_function_result,
            new_callable=AsyncMock,
            
        ) as mock_process_function_call,
    ):
        response: list[ChatMessageContent] = await chat_completion_base.get_chat_message_contents(
            chat_history=ChatHistory(system_message="Test"), settings=mock_settings, kernel=kernel, arguments=arguments
        )
        
        # Check for Function Call Behavior Auto
        if function_choice_behavior.type_ == 'auto':
            if function_choice_behavior.auto_invoke_kernel_functions:
                # Check if the function call was invoked
                mock_process_function_call.assert_awaited()
                assert response == [mock_message_text_content]
            else:
                # Check if the function call was not invoked
                assert response == [mock_message_function_call]
            
        # Check for Function Call Behavior None
        if function_choice_behavior.type_ == FunctionChoiceType.NONE:
            # Always returns Text
            assert response == [mock_message_text_content]
          
        # Check for Function Call Behavior Required  
        if function_choice_behavior.type_ == FunctionChoiceType.REQUIRED:
            if function_choice_behavior.auto_invoke_kernel_functions:
                # Check if the function call was invoked
                mock_process_function_call.assert_awaited()
                assert response == [mock_message_function_result]
            else:
                # Check if the function call was not invoked
                assert response == [mock_message_function_call]


@pytest.mark.asyncio
async def test_complete_chat_contents_function_call_behavior_without_kernel(
    mock_settings: MistralAIChatPromptExecutionSettings,
    mock_mistral_ai_client_completion: MistralAsyncClient,
):
    chat_history = MagicMock()
    chat_completion_base = MistralAIChatCompletion(
        ai_model_id="test_model_id", service_id="test", api_key="", async_client=mock_mistral_ai_client_completion
    )
    
    mock_settings.function_choice_behavior = FunctionChoiceBehavior.Auto()

    with pytest.raises(ServiceInvalidExecutionSettingsError):
        await chat_completion_base.get_chat_message_contents(
            chat_history=chat_history, settings=mock_settings
        )


@pytest.mark.asyncio
async def test_mistral_ai_sdk_exception_streaming(kernel: Kernel, mock_settings: MistralAIChatPromptExecutionSettings):
    chat_history = MagicMock()
    arguments = KernelArguments()
    client = MagicMock(spec=MistralAsyncClient)
    client.chat_stream.side_effect = Exception("Test Exception")

    chat_completion_base = MistralAIChatCompletion(
        ai_model_id="test_model_id", service_id="test", api_key="", async_client=client
    )

    with pytest.raises(ServiceResponseException):
        async for content in chat_completion_base.get_streaming_chat_message_contents(
            chat_history, mock_settings, kernel=kernel, arguments=arguments
        ):
            assert content is not None


def test_mistral_ai_chat_completion_init(mistralai_unit_test_env) -> None:
    # Test successful initialization
    mistral_ai_chat_completion = MistralAIChatCompletion()

    assert mistral_ai_chat_completion.ai_model_id == mistralai_unit_test_env["MISTRALAI_CHAT_MODEL_ID"]
    assert mistral_ai_chat_completion.async_client._api_key == mistralai_unit_test_env["MISTRALAI_API_KEY"]
    assert isinstance(mistral_ai_chat_completion, ChatCompletionClientBase)


@pytest.mark.parametrize("exclude_list", [["MISTRALAI_API_KEY", "MISTRALAI_CHAT_MODEL_ID"]], indirect=True)
def test_mistral_ai_chat_completion_init_constructor(mistralai_unit_test_env) -> None:
    # Test successful initialization
    mistral_ai_chat_completion = MistralAIChatCompletion(
        api_key="overwrite_api_key",
        ai_model_id="overwrite_model_id",
        env_file_path="test.env",
    )

    assert mistral_ai_chat_completion.ai_model_id == "overwrite_model_id"
    assert mistral_ai_chat_completion.async_client._api_key == "overwrite_api_key"
    assert isinstance(mistral_ai_chat_completion, ChatCompletionClientBase)


@pytest.mark.parametrize("exclude_list", [["MISTRALAI_API_KEY", "MISTRALAI_CHAT_MODEL_ID"]], indirect=True)
def test_mistral_ai_chat_completion_init_constructor_missing_model(mistralai_unit_test_env) -> None:
    # Test successful initialization
    with pytest.raises(ServiceInitializationError):
        MistralAIChatCompletion(
            api_key="overwrite_api_key",
            env_file_path="test.env"
        )


@pytest.mark.parametrize("exclude_list", [["MISTRALAI_API_KEY", "MISTRALAI_CHAT_MODEL_ID"]], indirect=True)
def test_mistral_ai_chat_completion_init_constructor_missing_api_key(mistralai_unit_test_env) -> None:
    # Test successful initialization
    with pytest.raises(ServiceInitializationError):
        MistralAIChatCompletion(
            ai_model_id="overwrite_model_id",
            env_file_path="test.env"
        )


def test_mistral_ai_chat_completion_init_hybrid(mistralai_unit_test_env) -> None:
    mistral_ai_chat_completion = MistralAIChatCompletion(
            ai_model_id="overwrite_model_id",
            env_file_path="test.env",
    )
    assert mistral_ai_chat_completion.ai_model_id == "overwrite_model_id"
    assert mistral_ai_chat_completion.async_client._api_key == "test_api_key"


@pytest.mark.parametrize("exclude_list", [["MISTRALAI_CHAT_MODEL_ID"]], indirect=True)
def test_mistral_ai_chat_completion_init_with_empty_model_id(mistralai_unit_test_env) -> None:
    with pytest.raises(ServiceInitializationError):
        MistralAIChatCompletion(
            env_file_path="test.env",
        )


def test_prompt_execution_settings_class(mistralai_unit_test_env):
    mistral_ai_chat_completion = MistralAIChatCompletion()
    prompt_execution_settings = mistral_ai_chat_completion.get_prompt_execution_settings_class()
    assert prompt_execution_settings == MistralAIChatPromptExecutionSettings


@pytest.mark.asyncio
async def test_with_different_execution_settings(kernel: Kernel, mock_mistral_ai_client_completion: MagicMock):
    chat_history = MagicMock()
    settings = OpenAIChatPromptExecutionSettings(temperature=0.2, seed=2)
    arguments = KernelArguments()
    chat_completion_base = MistralAIChatCompletion(
        ai_model_id="test_model_id", service_id="test", api_key="", async_client=mock_mistral_ai_client_completion
    )

    await chat_completion_base.get_chat_message_contents(
        chat_history=chat_history, settings=settings, kernel=kernel, arguments=arguments
    )
    assert mock_mistral_ai_client_completion.chat.call_args.kwargs["temperature"] == 0.2
    assert mock_mistral_ai_client_completion.chat.call_args.kwargs["seed"] == 2


@pytest.mark.asyncio
async def test_with_different_execution_settings_stream(
    kernel: Kernel, mock_mistral_ai_client_completion_stream: MagicMock
):
    chat_history = MagicMock()
    settings = OpenAIChatPromptExecutionSettings(temperature=0.2, seed=2)
    arguments = KernelArguments()
    chat_completion_base = MistralAIChatCompletion(
        ai_model_id="test_model_id",
        service_id="test",
        api_key="",
        async_client=mock_mistral_ai_client_completion_stream,
    )

    async for chunk in chat_completion_base.get_streaming_chat_message_contents(
        chat_history, settings, kernel=kernel, arguments=arguments
    ):
        continue
    assert mock_mistral_ai_client_completion_stream.chat_stream.call_args.kwargs["temperature"] == 0.2
    assert mock_mistral_ai_client_completion_stream.chat_stream.call_args.kwargs["seed"] == 2

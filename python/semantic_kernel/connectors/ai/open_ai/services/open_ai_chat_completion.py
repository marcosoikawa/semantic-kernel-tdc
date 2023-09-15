# Copyright (c) Microsoft. All rights reserved.

from logging import Logger
from typing import Any, Dict, List, Optional, Tuple, Union

from openai import AsyncOpenAI

# if TYPE_CHECKING:
#     from openai.openai_object import OpenAIObject
# import openai
from pydantic import HttpUrl

from semantic_kernel.connectors.ai.ai_exception import AIException
from semantic_kernel.connectors.ai.chat_completion_client_base import (
    ChatCompletionClientBase,
)
from semantic_kernel.connectors.ai.chat_request_settings import ChatRequestSettings
from semantic_kernel.connectors.ai.complete_request_settings import (
    CompleteRequestSettings,
)
from semantic_kernel.connectors.ai.open_ai.models.chat.function_call import FunctionCall
from semantic_kernel.connectors.ai.text_completion_client_base import (
    TextCompletionClientBase,
)

# from semantic_kernel.utils.null_logger import NullLogger


class OpenAIChatCompletionBase(ChatCompletionClientBase, TextCompletionClientBase):
    model_id: str
    api_key: str
    api_type: str
    org_id: Optional[str] = None
    api_version: Optional[str] = None
    endpoint: Optional[HttpUrl] = None

    async def complete_chat_async(
        self,
        messages: List[Dict[str, str]],
        request_settings: ChatRequestSettings,
        logger: Optional[Logger] = None,
    ) -> Union[str, List[str]]:
        # TODO: tracking on token counts/etc.
        response = await self._send_chat_request(
            messages, request_settings, False, None
        )

        if len(response.choices) == 1:
            return response.choices[0].message.content
        return [choice.message.content for choice in response.choices]

    async def complete_chat_with_functions_async(
        self,
        messages: List[Dict[str, str]],
        functions: List[Dict[str, Any]],
        request_settings: ChatRequestSettings,
        logger: Optional[Logger] = None,
    ) -> Union[
        Tuple[Optional[str], Optional[FunctionCall]],
        List[Tuple[Optional[str], Optional[FunctionCall]]],
    ]:
        # TODO: tracking on token counts/etc.

        response = await self._send_chat_request(
            messages, request_settings, False, functions
        )

        if len(response.choices) == 1:
            return _parse_message(response.choices[0].message, self._log)
        else:
            return [
                _parse_message(choice.message, self._log) for choice in response.choices
            ]

    async def complete_chat_stream_async(
        self,
        messages: List[Dict[str, str]],
        request_settings: ChatRequestSettings,
    ):
        # TODO: enable function calling
        response = await self._send_chat_request(messages, request_settings, True, None)

        # parse the completion text(s) and yield them
        async for chunk in response:
            text, index = _parse_choices(chunk)
            # if multiple responses are requested, keep track of them
            if request_settings.number_of_responses > 1:
                completions = [""] * request_settings.number_of_responses
                completions[index] = text
                yield completions
            # if only one response is requested, yield it
            else:
                yield text

    async def complete_async(
        self,
        prompt: str,
        request_settings: CompleteRequestSettings,
        logger: Optional[Logger] = None,
    ) -> Union[str, List[str]]:
        """
        Completes the given prompt.

        Arguments:
            prompt {str} -- The prompt to complete.
            request_settings {CompleteRequestSettings} -- The request settings.

        Returns:
            str -- The completed text.
        """
        prompt_to_message = [{"role": "user", "content": prompt}]
        chat_settings = ChatRequestSettings.from_completion_config(request_settings)
        response = await self._send_chat_request(
            prompt_to_message, chat_settings, False
        )

        if len(response.choices) == 1:
            return response.choices[0].message.content
        else:
            return [choice.message.content for choice in response.choices]

    async def complete_stream_async(
        self,
        prompt: str,
        request_settings: CompleteRequestSettings,
        logger: Optional[Logger] = None,
    ):
        prompt_to_message = [{"role": "user", "content": prompt}]
        chat_settings = ChatRequestSettings(
            temperature=request_settings.temperature,
            top_p=request_settings.top_p,
            presence_penalty=request_settings.presence_penalty,
            frequency_penalty=request_settings.frequency_penalty,
            max_tokens=request_settings.max_tokens,
            number_of_responses=request_settings.number_of_responses,
            token_selection_biases=request_settings.token_selection_biases,
            stop_sequences=request_settings.stop_sequences,
        )
        response = await self._send_chat_request(prompt_to_message, chat_settings, True)

        # parse the completion text(s) and yield them
        async for chunk in response:
            text, index = _parse_choices(chunk)
            # if multiple responses are requested, keep track of them
            if request_settings.number_of_responses > 1:
                completions = [""] * request_settings.number_of_responses
                completions[index] = text
                yield completions
            # if only one response is requested, yield it
            else:
                yield text

    async def _send_chat_request(
        self,
        messages: List[Tuple[str, str]],
        request_settings: ChatRequestSettings,
        stream: bool,
        functions: Optional[List[Dict[str, Any]]] = None,
    ):
        """
        Completes the given user message with an asynchronous stream.

        Arguments:
            messages {List[Tuple[str,str]]} -- The messages (from a user) to respond to.
            request_settings {ChatRequestSettings} -- The request settings.
            stream {bool} -- Whether to stream the response.
            functions {List[Dict[str, Any]]} -- The functions available to the api.

        Returns:
            str -- The completed text.
        """
        if request_settings is None:
            raise ValueError("The request settings cannot be `None`")

        if request_settings.max_tokens < 1:
            raise AIException(
                AIException.ErrorCodes.InvalidRequest,
                "The max tokens must be greater than 0, "
                f"but was {request_settings.max_tokens}",
            )

        if len(messages) <= 0:
            raise AIException(
                AIException.ErrorCodes.InvalidRequest,
                "To complete a chat you need at least one message",
            )

        if messages[-1]["role"] in ["assistant", "system"]:
            raise AIException(
                AIException.ErrorCodes.InvalidRequest,
                "The last message must be from the user or a function output",
            )

        model_args = {
            "engine"
            if self._api_type in ["azure", "azure_ad"]
            else "model": self._model_id,
            "messages": messages,
            "temperature": request_settings.temperature,
            "top_p": request_settings.top_p,
            "n": request_settings.number_of_responses,
            "stream": stream,
            "stop": (
                request_settings.stop_sequences
                if request_settings.stop_sequences is not None
                and len(request_settings.stop_sequences) > 0
                else None
            ),
            "max_tokens": request_settings.max_tokens,
            "presence_penalty": request_settings.presence_penalty,
            "frequency_penalty": request_settings.frequency_penalty,
            "logit_bias": (
                request_settings.token_selection_biases
                if request_settings.token_selection_biases is not None
                and len(request_settings.token_selection_biases) > 0
                else {}
            ),
        }
        if self.api_type in ["azure", "azure_ad"]:
            model_args["engine"] = self.model_id
        else:
            model_args["model"] = self.model_id

        if functions and request_settings.function_call is not None:
            model_args["function_call"] = request_settings.function_call
            if request_settings.function_call != "auto":
                model_args["functions"] = [
                    func
                    for func in functions
                    if func["name"] == request_settings.function_call
                ]
            else:
                model_args["functions"] = functions

        try:
            client = AsyncOpenAI(
                api_key=self._api_key,
                base_url=self._endpoint,
                organization=self._org_id,
                version=self._api_version
            )
            response: Any = await client.chat.completions.create(**model_args)
        except Exception as ex:
            raise AIException(
                AIException.ErrorCodes.ServiceError,
                f"{self.__class__.__name__} failed to complete the chat",
                ex,
            ) from ex

        # streaming does not have usage info, therefore checking the type of the response
        if not stream and "usage" in response:
            self._log.info(
                f"OpenAI service used {response.usage} tokens for this request"
            )
            self.capture_usage_details(**response.usage)

        return response

    @property
    def prompt_tokens(self) -> int:
        return self._prompt_tokens

    @property
    def completion_tokens(self) -> int:
        return self._completion_tokens

    @property
    def total_tokens(self) -> int:
        return self._total_tokens


class OpenAIChatCompletion(OpenAIChatCompletionBase):
    def __init__(
        self,
        model_id: str,
        api_key: str,
        org_id: Optional[str] = None,
        log: Optional[Logger] = None,
    ) -> None:
        """
        Initialize an OpenAIChatCompletion service.

        Arguments:
            model_id {str} -- OpenAI model name, see
                https://platform.openai.com/docs/models
            api_key {str} -- OpenAI API key, see
                https://platform.openai.com/account/api-keys
            org_id {Optional[str]} -- OpenAI organization ID.
                This is usually optional unless your
                account belongs to multiple organizations.
            log {Optional[Logger]} -- The logger instance to use. (Optional)
        """
        kwargs = {
            "model_id": model_id,
            "api_key": api_key,
            "org_id": org_id,
            "api_type": "open_ai",
        }
        if log:
            kwargs["log"] = log
        super().__init__(**kwargs)

    def to_dict(self) -> Dict[str, str]:
        """
        Create a dict of the service settings.
        """
        # TODO: figure out if we need to be able to reinitialize the token counters.
        return self.dict(
            exclude={
                "prompt_tokens",
                "completion_tokens",
                "total_tokens",
                "api_version",
                "endpoint",
                "api_type",
            },
            by_alias=True,
            exclude_none=True,
        )


def _parse_choices(chunk):
    message = ""
    if "role" in chunk.choices[0].delta:
        message += chunk.choices[0].delta.role + ": "
    if "content" in chunk.choices[0].delta:
        message += chunk.choices[0].delta.content
    if "function_call" in chunk.choices[0].delta:
        message += chunk.choices[0].delta.function_call

    index = chunk.choices[0].index
    return message, index


def _parse_message(
    message: "OpenAIObject", logger: Optional[Logger] = None
) -> Tuple[Optional[str], Optional[FunctionCall]]:
    """
    Parses the message.

    Arguments:
        message {OpenAIObject} -- The message to parse.

    Returns:
        Tuple[Optional[str], Optional[Dict]] -- The parsed message.
    """
    content = message.content if hasattr(message, "content") else None
    function_call = message.function_call if hasattr(message, "function_call") else None
    if function_call:
        function_call = FunctionCall(
            name=function_call.name,
            arguments=function_call.arguments,
        )

    return (content, function_call)

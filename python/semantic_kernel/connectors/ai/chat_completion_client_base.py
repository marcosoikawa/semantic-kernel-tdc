# Copyright (c) Microsoft. All rights reserved.

from abc import ABC, abstractmethod
from typing import TYPE_CHECKING, Any, AsyncIterable, List, Optional

from semantic_kernel.services.ai_service_client_base import AIServiceClientBase

if TYPE_CHECKING:
    from semantic_kernel.connectors.ai.prompt_execution_settings import PromptExecutionSettings
    from semantic_kernel.contents import ChatMessageContent, StreamingChatMessageContent
    from semantic_kernel.models.ai.chat_completion.chat_message import ChatMessage


class ChatCompletionClientBase(AIServiceClientBase, ABC):
    @abstractmethod
    async def complete_chat(
        self,
        messages: List["ChatMessage"],
        settings: "PromptExecutionSettings",
        logger: Optional[Any] = None,
    ) -> List["ChatMessageContent"]:
        """
        This is the method that is called from the kernel to get a response from a chat-optimized LLM.

        Arguments:
            messages {List[ChatMessage]} -- A list of chat messages, that can be rendered into a
                set of messages, from system, user, assistant and function.
            settings {PromptExecutionSettings} -- Settings for the request.
            logger {Logger} -- A logger to use for logging. (Deprecated)

        Returns:
            Union[str, List[str]] -- A string or list of strings representing the response(s) from the LLM.
        """
        pass

    @abstractmethod
    async def complete_chat_stream(
        self,
        messages: List["ChatMessage"],
        settings: "PromptExecutionSettings",
        logger: Optional[Any] = None,
    ) -> AsyncIterable[List["StreamingChatMessageContent"]]:
        """
        This is the method that is called from the kernel to get a stream response from a chat-optimized LLM.

        Arguments:
            messages {List[ChatMessage]} -- A list of chat messages, that can be rendered into a
                set of messages, from system, user, assistant and function.
            settings {PromptExecutionSettings} -- Settings for the request.
            logger {Logger} -- A logger to use for logging. (Deprecated)

        Yields:
            A stream representing the response(s) from the LLM.
        """
        pass

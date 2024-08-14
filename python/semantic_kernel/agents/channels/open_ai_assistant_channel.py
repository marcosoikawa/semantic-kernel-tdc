# Copyright (c) Microsoft. All rights reserved.

from collections.abc import AsyncIterable
from typing import TYPE_CHECKING, Any

from openai import AsyncOpenAI

from semantic_kernel.agents.channels.agent_channel import AgentChannel
from semantic_kernel.contents.chat_message_content import ChatMessageContent
from semantic_kernel.exceptions.agent_exceptions import AgentChatException

if TYPE_CHECKING:
    from semantic_kernel.agents.agent import Agent


class OpenAIAssistantChannel(AgentChannel):
    """OpenAI Assistant Channel."""

    def __init__(self, client: AsyncOpenAI, thread_id: str) -> None:
        """Initialize the OpenAI Assistant Channel."""
        self.client = client
        self.thread_id = thread_id

    async def receive(self, history: list["ChatMessageContent"]) -> None:
        """Receive the conversation messages."""
        from semantic_kernel.agents.open_ai.open_ai_assistant_base import OpenAIAssistantBase

        for message in history:
            await OpenAIAssistantBase.create_chat_message(self.client, self.thread_id, message)

    async def invoke(self, agent: "Agent") -> AsyncIterable[tuple[bool, "ChatMessageContent"]]:
        """Invoke the agent."""
        from semantic_kernel.agents.open_ai.open_ai_assistant_base import OpenAIAssistantBase

        if not isinstance(agent, OpenAIAssistantBase):
            raise AgentChatException(f"Agent is not of the expected type {type(OpenAIAssistantBase)}.")

        if agent._is_deleted:
            raise AgentChatException("Agent is deleted.")

        async for is_visible, message in agent._invoke_internal(thread_id=self.thread_id):
            yield is_visible, message

    async def get_history(self) -> AsyncIterable["ChatMessageContent"]:
        """Get the conversation history."""
        from semantic_kernel.agents.open_ai.open_ai_assistant_base import OpenAIAssistantBase

        agent_names: dict[str, Any] = {}

        thread_messages = await self.client.beta.threads.messages.list(
            thread_id=self.thread_id, limit=100, order="desc"
        )
        for message in thread_messages.data:
            assistant_name = None
            if message.assistant_id and message.assistant_id not in agent_names:
                agent = await self.client.beta.assistants.retrieve(message.assistant_id)
                if agent.name:
                    agent_names[message.assistant_id] = agent.name
            assistant_name = agent_names.get(message.assistant_id) if message.assistant_id else message.assistant_id
            assistant_name = assistant_name or message.assistant_id

            content: ChatMessageContent = OpenAIAssistantBase._generate_message_content(str(assistant_name), message)

            if len(content.items) > 0:
                yield content

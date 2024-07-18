# Copyright (c) Microsoft. All rights reserved.

import asyncio
import logging
from collections.abc import AsyncIterable
from typing import TYPE_CHECKING, Any, ClassVar

from openai import AsyncOpenAI
from openai.resources.beta.assistants import Assistant
from openai.resources.beta.threads.runs.runs import Run
from pydantic import ValidationError

from semantic_kernel.agents.agent import Agent
from semantic_kernel.agents.openai.open_ai_assistant_configuration import OpenAIAssistantConfiguration
from semantic_kernel.agents.openai.open_ai_assistant_definition import OpenAIAssistantDefinition
from semantic_kernel.agents.openai.open_ai_thread_creation_settings import OpenAIThreadCreationSettings
from semantic_kernel.connectors.ai.function_calling_utils import kernel_function_metadata_to_function_call_format
from semantic_kernel.connectors.ai.open_ai.settings.open_ai_settings import OpenAISettings
from semantic_kernel.const import DEFAULT_SERVICE_NAME
from semantic_kernel.contents import AuthorRole
from semantic_kernel.contents.chat_history import ChatHistory
from semantic_kernel.contents.chat_message_content import ChatMessageContent
from semantic_kernel.contents.function_call_content import FunctionCallContent
from semantic_kernel.exceptions.service_exceptions import ServiceInitializationError, ServiceResponseException

if TYPE_CHECKING:
    from openai.resources.beta.threads.messages import Message

    from semantic_kernel.kernel import Kernel


logger: logging.Logger = logging.getLogger(__name__)


class OpenAIAssistantAgent(Agent):
    ai_model_id: str
    assistant: Assistant | None = None
    client: AsyncOpenAI | None = None
    config: OpenAIAssistantConfiguration | None = None
    definition: OpenAIAssistantDefinition | None = None

    allowed_message_roles: ClassVar[list[str]] = [AuthorRole.USER, AuthorRole.ASSISTANT]
    error_message_states: ClassVar[list[str]] = ["failed", "canceled", "expired"]

    _is_deleted: bool = False

    # region Agent Initialization

    def __init__(
        self,
        api_key: str | None = None,
        org_id: str | None = None,
        name: str | None = None,
        description: str | None = None,
        id: str | None = None,
        instructions: str | None = None,
        kernel: "Kernel | None" = None,
        configuration: OpenAIAssistantConfiguration | None = None,
        definition: OpenAIAssistantDefinition | None = None,
        ai_model_id: str | None = None,
        service_id: str | None = None,
        client: AsyncOpenAI | None = None,
        env_file_path: str | None = None,
        env_file_encoding: str | None = None,
    ) -> None:
        """Initialize an OpenAIAssistant service."""
        if configuration is None:
            configuration = OpenAIAssistantConfiguration()

        if definition is None:
            definition = OpenAIAssistantDefinition()

        try:
            openai_settings = OpenAISettings.create(
                api_key=api_key,
                org_id=org_id,
                chat_model_id=ai_model_id,
                env_file_path=env_file_path,
                env_file_encoding=env_file_encoding,
            )
        except ValidationError as ex:
            raise ServiceInitializationError("Failed to create OpenAI settings.", ex) from ex

        if not client and not openai_settings.api_key:
            raise ServiceInitializationError("The OpenAI API key is required.")
        if not openai_settings.chat_model_id:
            raise ServiceInitializationError("The OpenAI model ID is required.")

        service_id = service_id or DEFAULT_SERVICE_NAME

        if client is None:
            client = AsyncOpenAI(
                api_key=openai_settings.api_key.get_secret_value() if openai_settings.api_key else None,
                organization=openai_settings.org_id,
            )

        args: dict[str, Any] = {
            "ai_model_id": openai_settings.chat_model_id,
            "service_id": service_id,
            "client": client,
            "name": name,
            "description": description,
            "instructions": instructions,
            "config": configuration,
            "definition": definition,
        }

        if id is not None:
            args["id"] = id
        if kernel is not None:
            args["kernel"] = kernel
        super().__init__(**args)

    @classmethod
    async def create(
        cls,
        *,
        name: str | None = None,
        description: str | None = None,
        id: str | None = None,
        instructions: str | None = None,
        kernel: "Kernel | None" = None,
        ai_model_id: str | None = None,
        service_id: str | None = None,
        api_key: str | None = None,
        org_id: str | None = None,
        client: AsyncOpenAI | None = None,
        env_file_path: str | None = None,
        env_file_encoding: str | None = None,
        configuration: OpenAIAssistantConfiguration | None = None,
        definition: OpenAIAssistantDefinition | None = None,
    ) -> "OpenAIAssistantAgent":
        """Asynchronous class method used to create the OpenAI Assistant Agent."""
        agent = cls(
            name=name,
            description=description,
            id=id,
            instructions=instructions,
            kernel=kernel,
            configuration=configuration,
            definition=definition,
            ai_model_id=ai_model_id,
            service_id=service_id,
            api_key=api_key,
            org_id=org_id,
            client=client,
            env_file_path=env_file_path,
            env_file_encoding=env_file_encoding,
        )
        agent.assistant = await agent._create_assistant()
        return agent

    async def _create_assistant(self) -> Assistant:
        """Create the assistant."""
        self.assistant = await self.client.beta.assistants.create(
            model=self.ai_model_id,
            instructions=self.instructions,
            name=self.name,
        )
        return self.assistant

    # endregion

    # region Agent Properties

    @property
    def file_ids(self) -> list[str]:
        """The file ids."""
        return self.assistant.file_ids

    @property
    def metadata(self) -> dict[str, str]:
        """The metadata."""
        return self.assistant.metadata

    @property
    def tools(self) -> dict[str, str]:
        """The tools."""
        return self.assistant.tools

    # endregion

    # region Agent Methods

    async def create_thread(self, thread_creation_settings: OpenAIThreadCreationSettings | None = None) -> str:
        """Create a thread.

        Args:
            thread_creation_settings (OpenAIThreadCreationSettings): The thread creation settings. Defaults to None.

        Returns:
            str: The thread id.
        """
        create_kwargs = {}

        thread = await self.client.beta.threads.create(**create_kwargs)
        return thread.id

    async def delete_thread(self, thread_id: str) -> None:
        """Delete a thread."""
        await self.client.beta.threads.delete(thread_id)

    async def delete(self) -> bool:
        """Delete the assistant."""
        if not self._is_deleted:
            await self.client.beta.assistants.delete(self.assistant.id)
        return self._is_deleted

    async def add_file(self, file_path: str) -> str:
        """Add a file.

        Args:
            file_path (str): The file path.

        Returns:
            str: The file id.
        """
        with open(file_path, "rb") as file:
            file = await self.client.files.create(file=file)
            return file.id

    async def invoke(self, thread_id: str) -> AsyncIterable[ChatMessageContent]:
        """Invoke the chat assistant."""
        self._check_if_deleted()
        tools = self._get_tools()

        try:
            run = await self._create_and_poll_run(thread_id, tools)
        except ServiceResponseException as ex:
            logger.error(f"Failed to create and poll run for assistant {self.assistant.id}.", ex)
            raise ServiceInitializationError("Failed to create and poll run.") from ex

        if run.status == "requires_action":
            await self._handle_function_calls(run, thread_id)
            run = await self._poll_run_status(run, thread_id)

        async for message in self._yield_assistant_messages(thread_id):
            yield message

    async def add_chat_message(self, thread_id: str, message: ChatMessageContent) -> "Message":
        """Add a chat message."""
        if message.role not in self.allowed_message_roles:
            raise ServiceInitializationError(
                f"Invalid message role `{message.role}`. Allowed roles are {self.allowed_message_roles}."
            )
        return await self.client.beta.threads.messages.create(
            thread_id=thread_id, role=message.role, content=message.content
        )

    def _check_if_deleted(self) -> None:
        """Check if the assistant has been deleted."""
        if self._is_deleted:
            raise ServiceInitializationError("The assistant has been deleted.")

    def _get_tools(self) -> list[dict[str, str]]:
        """Get the list of tools for the assistant."""
        tools = []
        if self.definition.enable_code_interpreter:
            tools.append({"type": "code_interpreter"})
        if self.definition.enable_file_search:
            tools.append({"type": "file_search"})
        funcs = self.kernel.get_full_list_of_function_metadata()
        tools.extend([kernel_function_metadata_to_function_call_format(f) for f in funcs])
        return tools

    async def _create_and_poll_run(self, thread_id: str, tools: list[dict[str, str]]) -> Run:
        """Create and poll the run for the assistant."""
        run = await self.client.beta.threads.runs.create_and_poll(
            assistant_id=self.assistant.id,
            thread_id=thread_id,
            instructions=self.assistant.instructions,
            tools=tools,
        )
        if run.status in self.error_message_states:
            raise ServiceResponseException(f"Failed to create and run: `{run.status}` for agent {self.assistant.id}.")
        return run

    async def _handle_function_calls(self, run: Run, thread_id: str) -> None:
        """Handle function calls required by the assistant."""
        fccs = self._get_function_call_contents(run)
        chat_history = ChatHistory()
        _ = await self._invoke_function_calls(fccs, chat_history)
        # do we support function invoke filter results?
        tool_outputs = self._format_tool_outputs(chat_history)
        await self.client.beta.threads.runs.submit_tool_outputs(
            run_id=run.id, thread_id=thread_id, tool_outputs=tool_outputs
        )

    def _get_function_call_contents(self, run: Run) -> list[FunctionCallContent]:
        """Extract function call contents from the run."""
        fccs = []
        for tool in run.required_action.submit_tool_outputs.tool_calls:
            fcc = FunctionCallContent(
                id=tool.id,
                index=getattr(tool, "index", None),
                name=tool.function.name,
                arguments=tool.function.arguments,
            )
            fccs.append(fcc)
        return fccs

    async def _invoke_function_calls(self, fccs: list[FunctionCallContent], chat_history: ChatHistory) -> list[Any]:
        """Invoke function calls and store results in chat history."""
        results = []
        for function_call in fccs:
            result = await self.kernel.invoke_function_call(
                function_call=function_call,
                chat_history=chat_history,
            )
            results.append(result)
        return results

    def _format_tool_outputs(self, chat_history: ChatHistory) -> list[dict[str, str]]:
        """Format tool outputs from chat history for submission."""
        tool_outputs = []
        for tool_call in chat_history.messages[0].items:
            tool_outputs.append(
                {
                    "tool_call_id": tool_call.id,
                    "output": tool_call.result,
                }
            )
        return tool_outputs

    async def _poll_run_status(self, run: Run, thread_id: str) -> Run:
        """Poll the run status until it completes or errors out."""
        while run.status not in self.error_message_states:
            await asyncio.sleep(1)
            run = await self.client.beta.threads.runs.retrieve(run.id, thread_id=thread_id)
            if run.status == "completed":
                break
        return run

    async def _yield_assistant_messages(self, thread_id: str) -> AsyncIterable[ChatMessageContent]:
        """Yield assistant messages from the thread."""
        messages = await self.client.beta.threads.messages.list(thread_id=thread_id)
        assistant_messages = [next(message for message in messages.data if message.role == "assistant")]
        for message in assistant_messages:
            yield ChatMessageContent(content=message.content[0].text.value, role=message.role)

    # endregion

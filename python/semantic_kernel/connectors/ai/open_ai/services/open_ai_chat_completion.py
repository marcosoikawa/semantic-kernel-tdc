# Copyright (c) Microsoft. All rights reserved.

from logging import Logger
from typing import Dict, Optional

from semantic_kernel.connectors.ai.open_ai.services.open_ai_chat_completion_base import (
    OpenAIChatCompletionBase,
)
from semantic_kernel.connectors.ai.open_ai.services.open_ai_config_base import (
    OpenAIConfigBase,
)
from semantic_kernel.connectors.ai.open_ai.services.open_ai_handler import (
    OpenAIModelTypes,
)
from semantic_kernel.connectors.ai.open_ai.services.open_ai_text_completion_base import (
    OpenAITextCompletionBase,
)


class OpenAIChatCompletion(
    OpenAIConfigBase, OpenAIChatCompletionBase, OpenAITextCompletionBase
):
    """OpenAI Chat completion class."""

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
        super().__init__(
            model_id=model_id,
            api_key=api_key,
            org_id=org_id,
            model_type=OpenAIModelTypes.CHAT,
            log=log,
        )

    @classmethod
    def from_dict(cls, settings: Dict[str, str]) -> "OpenAIChatCompletion":
        """
        Initialize an Open AI service from a dictionary of settings.

        Arguments:
            settings: A dictionary of settings for the service.
        """

        return OpenAIChatCompletion(
            model_id=settings["model_id"],
            api_key=settings["api_key"],
            org_id=settings.get("org_id"),
            log=settings.get("log"),
        )

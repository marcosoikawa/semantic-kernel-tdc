# Copyright (c) Microsoft. All rights reserved.

import logging
from typing import Mapping

from openai import AsyncAzureOpenAI
from openai.lib.azure import AsyncAzureADTokenProvider
from pydantic import ValidationError
from semantic_kernel.connectors.ai.open_ai.const import DEFAULT_AZURE_API_VERSION
from semantic_kernel.connectors.ai.open_ai.services.azure_config_base import (
    AzureOpenAIConfigBase,
)
from semantic_kernel.connectors.ai.open_ai.services.open_ai_handler import (
    OpenAIModelTypes,
)
from semantic_kernel.connectors.ai.open_ai.services.open_ai_text_completion_base import (
    OpenAITextCompletionBase,
)
from semantic_kernel.connectors.ai.settings.azure_open_ai_settings import AzureOpenAISettings
from semantic_kernel.kernel_pydantic import HttpsUrl
from semantic_kernel.exceptions.service_exceptions import ServiceInitializationError

logger: logging.Logger = logging.getLogger(__name__)


class AzureTextCompletion(AzureOpenAIConfigBase, OpenAITextCompletionBase):
    """Azure Text Completion class."""

    def __init__(
        self,
        service_id: str | None = None,
        ad_token: str | None = None,
        ad_token_provider: AsyncAzureADTokenProvider | None = None,
        default_headers: Mapping[str, str] | None = None,
        async_client: AsyncAzureOpenAI | None = None,
        use_env_settings_file: bool = False,
    ) -> None:
        """
        Initialize an AzureTextCompletion service.

        Arguments:
            service_id: The service ID for the Azure deployment. (Optional)
            ad_token: The Azure Active Directory token. (Optional)
            ad_token_provider: The Azure Active Directory token provider. (Optional)
            default_headers: The default headers mapping of string keys to
                string values for HTTP requests. (Optional)
            async_client {Optional[AsyncAzureOpenAI]} -- An existing client to use. (Optional)
            use_env_settings_file {bool} -- Use the environment settings file as a fallback to
                environment variables. (Optional)
        """
        try:
            azure_openai_settings = AzureOpenAISettings(use_env_settings_file=use_env_settings_file)
        except ValidationError as e:
            logger.error(f"Failed to initialize AzureTextCompletion service: {e}")
            raise ServiceInitializationError("Failed to initialize AzureTextCompletion service") from e
        base_url = azure_openai_settings.base_url
        endpoint = azure_openai_settings.endpoint
        deployment_name = azure_openai_settings.text_deployment_name
        api_version = azure_openai_settings.api_version
        api_key = azure_openai_settings.api_key.get_secret_value()

        super().__init__(
            deployment_name=deployment_name,
            endpoint=endpoint if not isinstance(endpoint, str) else HttpsUrl(endpoint),
            base_url=base_url,
            api_version=api_version,
            service_id=service_id,
            api_key=api_key,
            ad_token=ad_token,
            ad_token_provider=ad_token_provider,
            default_headers=default_headers,
            ai_model_type=OpenAIModelTypes.TEXT,
            async_client=async_client,
        )

    @classmethod
    def from_dict(cls, settings: dict[str, str]) -> "AzureTextCompletion":
        """
        Initialize an Azure OpenAI service from a dictionary of settings.

        Arguments:
            settings: A dictionary of settings for the service.
                should contains keys: deployment_name, endpoint, api_key
                and optionally: api_version, ad_auth
        """

        return AzureTextCompletion(
            service_id=settings.get("service_id"),
            ad_token=settings.get("ad_token"),
            ad_token_provider=settings.get("ad_token_provider"),
            default_headers=settings.get("default_headers"),
            use_env_settings_file=settings.get("use_env_settings_file", False),
        )

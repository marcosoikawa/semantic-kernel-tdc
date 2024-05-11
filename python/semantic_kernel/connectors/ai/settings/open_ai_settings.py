# Copyright (c) Microsoft. All rights reserved.

from pydantic import SecretStr
from pydantic_settings import BaseSettings


class OpenAISettings(BaseSettings):
    """OpenAI model settings

    The settings are first loaded from environment variables with the prefix 'AZURE_OPENAI_'. If the
    environment variables are not found, the settings can be loaded from a .env file with the
    encoding 'utf-8'. If the settings are not found in the .env file, the settings are ignored;
    however, validation will fail alerting that the settings are missing.

    Required settings for prefix 'OPENAI_' are:
    - api_key: SecretStr - OpenAI API key, see https://platform.openai.com/account/api-keys
        (Env var OPENAI_API_KEY)

    Optional settings for prefix 'OPENAI_' are:
    - org_id: str | None - This is usually optional unless your account belongs to multiple organizations.
        (Env var OPENAI_ORG_ID)
    - ai_model_id: str | None - The OpenAI model ID to use. If not provided, the default model
        (gpt-3.5-turbo) is used. (Env var OPENAI_AI_MODEL_ID)
    """

    use_env_settings_file: bool = False
    org_id: str | None = None
    api_key: SecretStr
    ai_model_id: str = "gpt-3.5-turbo"

    class Config:
        env_prefix = "OPENAI_"
        env_file = None
        env_file_encoding = "utf-8"
        extra = "ignore"
        case_sensitive = False

    @classmethod
    def create(cls, **kwargs):
        if kwargs.pop("use_env_settings_file", False):
            cls.Config.env_file = ".env"
        return cls(**kwargs)

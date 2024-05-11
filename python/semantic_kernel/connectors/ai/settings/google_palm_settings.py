# Copyright (c) Microsoft. All rights reserved.

from pydantic import SecretStr
from pydantic_settings import BaseSettings


class GooglePalmSettings(BaseSettings):
    """Google Palm model settings

    The settings are first loaded from environment variables with the prefix 'GOOGLE_PALM_'. If the
    environment variables are not found, the settings can be loaded from a .env file with the
    encoding 'utf-8'. If the settings are not found in the .env file, the settings are ignored;
    however, validation will fail alerting that the settings are missing.

    Required settings for prefix 'GOOGLE_PALM_' are:
    - api_key: SecretStr - GooglePalm API key, see https://developers.generativeai.google/products/palm

    Optional settings:
    - use_env_settings_file: bool - Use the environment settings file as a fallback to environment variables. (Optional)
    """

    use_env_settings_file: bool = False
    api_key: SecretStr = None

    class Config:
        env_prefix = "GOOGLE_PALM_"
        env_file = None
        env_file_encoding = "utf-8"
        extra = "ignore"
        case_sensitive = False

    @classmethod
    def create(cls, **kwargs):
        if kwargs.pop("use_env_settings_file", False):
            cls.Config.env_file = ".env"
        return cls(**kwargs)

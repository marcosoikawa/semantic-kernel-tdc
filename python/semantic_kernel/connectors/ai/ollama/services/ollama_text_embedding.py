# Copyright (c) Microsoft. All rights reserved.

import logging
import sys
from typing import Any

from ollama import AsyncClient
from pydantic import ValidationError

from semantic_kernel.connectors.ai.ollama.ollama_prompt_execution_settings import OllamaEmbeddingPromptExecutionSettings
from semantic_kernel.connectors.ai.ollama.ollama_settings import OllamaSettings
from semantic_kernel.connectors.ai.ollama.services.ollama_base import OllamaBase
from semantic_kernel.exceptions.service_exceptions import ServiceInitializationError

if sys.version_info >= (3, 12):
    from typing import override  # pragma: no cover
else:
    from typing_extensions import override  # pragma: no cover

from numpy import array, ndarray

from semantic_kernel.connectors.ai.embeddings.embedding_generator_base import EmbeddingGeneratorBase
from semantic_kernel.utils.experimental_decorator import experimental_class

logger: logging.Logger = logging.getLogger(__name__)


@experimental_class
class OllamaTextEmbedding(OllamaBase, EmbeddingGeneratorBase):
    """Ollama embeddings client.

    Make sure to have the ollama service running either locally or remotely.
    """

    def __init__(
        self,
        service_id: str | None = None,
        ai_model_id: str | None = None,
        host: str | None = None,
        env_file_path: str | None = None,
        env_file_encoding: str | None = None,
    ) -> None:
        """Initialize an OllamaChatCompletion service.

        Args:
            service_id (Optional[str]): Service ID tied to the execution settings. (Optional)
            ai_model_id (Optional[str]): The model name. (Optional)
            host (Optional[str]): URL of the Ollama server, defaults to None and
                will use the default Ollama service address: http://127.0.0.1:11434. (Optional)
            env_file_path (str | None): Use the environment settings file as a fallback to using env vars.
            env_file_encoding (str | None): The encoding of the environment settings file, defaults to 'utf-8'.
        """
        try:
            ollama_settings = OllamaSettings.create(
                model=ai_model_id,
                host=host,
                env_file_path=env_file_path,
                env_file_encoding=env_file_encoding,
            )
        except ValidationError as ex:
            raise ServiceInitializationError("Failed to create Ollama settings.", ex) from ex

        super().__init__(
            service_id=service_id or ollama_settings.model,
            ai_model_id=ollama_settings.model,
            host=ollama_settings.host,
        )

    @override
    async def generate_embeddings(self, texts: list[str], **kwargs: Any) -> ndarray:
        settings: OllamaEmbeddingPromptExecutionSettings = kwargs.get(
            "settings",
            OllamaEmbeddingPromptExecutionSettings(),
        )

        result = []
        for text in texts:
            response_object = await AsyncClient(host=self.host).embeddings(
                model=self.ai_model_id,
                prompt=text,
                **settings.prepare_settings_dict(),
            )
            result.append(response_object["embedding"])

        return array(result)

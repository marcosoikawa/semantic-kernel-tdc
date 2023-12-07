# Copyright (c) Microsoft. All rights reserved.

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional

from semantic_kernel.semantic_functions.prompt_template_config import (
    PromptTemplateConfig,
)


@dataclass
class PromptTemplateWithDataConfig(PromptTemplateConfig):
    @dataclass
    class CompletionConfig(PromptTemplateConfig.CompletionConfig):
        inputLanguage: str = None
        outputLanguage: str = None
        data_source_settings: Optional[
            "PromptTemplateWithDataConfig.AzureChatWithDataSettings"
        ] = None

    @dataclass
    class AzureAISearchDataSourceParameters:
        """Class to hold Azure AI Search data source parameters."""

        indexName: str
        endpoint: str
        key: Optional[str] = None
        indexLanguage: Optional[str] = None
        fieldsMapping: Dict[str, Any] = field(default_factory=dict)
        inScope: Optional[bool] = True
        topNDocuments: Optional[int] = 5
        queryType: Optional[str] = "simple"
        semanticConfiguration: Optional[str] = None
        roleInformation: Optional[str] = None

    @dataclass
    class AzureChatWithDataSettings:
        """Class to hold Azure OpenAI Chat With Data settings,
        which might include data source type and authentication information."""

        data_source_type: str = "AzureCognitiveSearch"
        data_source_parameters: "PromptTemplateConfig.AzureAISearchDataSourceParameters" = (
            None
        )

    @staticmethod
    def from_dict(data: dict) -> "PromptTemplateWithDataConfig":
        config = super().from_dict(data)

        completion_keys = ["inputLanguage", "outputLanguage"]
        for comp_key in completion_keys:
            if comp_key in data["completion"]:
                setattr(config.completion, comp_key, data["completion"][comp_key])

        return config

    @staticmethod
    def from_completion_parameters(
        temperature: float = 0.0,
        top_p: float = 1.0,
        presence_penalty: float = 0.0,
        frequency_penalty: float = 0.0,
        max_tokens: int = 256,
        number_of_responses: int = 1,
        stop_sequences: List[str] = [],
        token_selection_biases: Dict[int, int] = {},
        chat_system_prompt: str = None,
        function_call: Optional[str] = None,
        inputLanguage: str = None,
        outputLanguage: str = None,
        data_source_settings: Optional[
            "PromptTemplateWithDataConfig.AzureChatWithDataSettings"
        ] = None,
    ) -> "PromptTemplateWithDataConfig":
        config = PromptTemplateConfig()
        config.completion.temperature = temperature
        config.completion.top_p = top_p
        config.completion.presence_penalty = presence_penalty
        config.completion.frequency_penalty = frequency_penalty
        config.completion.max_tokens = max_tokens
        config.completion.number_of_responses = number_of_responses
        config.completion.stop_sequences = stop_sequences
        config.completion.token_selection_biases = token_selection_biases
        config.completion.chat_system_prompt = chat_system_prompt
        config.completion.function_call = function_call
        config.completion.inputLanguage = inputLanguage
        config.completion.outputLanguage = outputLanguage
        config.completion.data_source_settings = data_source_settings
        return config

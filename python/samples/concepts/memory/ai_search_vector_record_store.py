# Copyright (c) Microsoft. All rights reserved.


import os
from dataclasses import dataclass, field
from typing import Annotated
from uuid import uuid4

from semantic_kernel import Kernel
from semantic_kernel.connectors.ai.open_ai.prompt_execution_settings.open_ai_prompt_execution_settings import (
    OpenAIEmbeddingPromptExecutionSettings,
)
from semantic_kernel.connectors.ai.open_ai.services.open_ai_text_embedding import OpenAITextEmbedding
from semantic_kernel.connectors.data.azure_ai_search.azure_ai_search_vector_record_store import (
    AzureAISearchVectorRecordStore,
)
from semantic_kernel.data.models.vector_store_model_decorator import vectorstoremodel
from semantic_kernel.data.models.vector_store_record_fields import (
    VectorStoreRecordDataField,
    VectorStoreRecordKeyField,
    VectorStoreRecordVectorField,
)


@vectorstoremodel
@dataclass
class MyDataModel:
    vector: Annotated[
        list[list[float]] | None,
        VectorStoreRecordVectorField(
            embedding_settings={"embedding": OpenAIEmbeddingPromptExecutionSettings(dimensions=1536)}
        ),
    ] = None
    other: str | None = None
    id: Annotated[str, VectorStoreRecordKeyField()] = field(default_factory=lambda: str(uuid4()))
    content: Annotated[str, VectorStoreRecordDataField(has_embedding=True, embedding_property_name="vector")] = (
        "content1"
    )


async def main():
    kernel = Kernel()
    kernel.add_service(OpenAITextEmbedding(service_id="embedding", ai_model_id="text-embedding-3-small"))
    async with AzureAISearchVectorRecordStore[MyDataModel](
        data_model_type=MyDataModel,
        collection_name=os.environ["ALT_SEARCH_INDEX_NAME"],
        search_endpoint=os.environ["ALT_SEARCH_ENDPOINT"],
        api_key=os.environ["ALT_SEARCH_API_KEY"],
        kernel=kernel,
    ) as record_store:
        record1 = MyDataModel(content="My text")
        record2 = MyDataModel(content="My other text")

        await record_store.upsert(record1)
        await record_store.upsert(record2)

        result = await record_store.get(record1.id)
        print(result)


if __name__ == "__main__":
    import asyncio

    asyncio.run(main())

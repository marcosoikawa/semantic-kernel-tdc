# Copyright (c) Microsoft. All rights reserved.

import numpy as np
import pytest

from semantic_kernel.connectors.memory.qdrant import QdrantMemoryStore
from semantic_kernel.memory.memory_record import MemoryRecord

try:
    import qdrant_client  # noqa: F401

    qdrant_client_installed = True
except ImportError:
    qdrant_client_installed = False

pytestmark = pytest.mark.skipif(
    not qdrant_client_installed, reason="qdrant-client is not installed"
)


@pytest.fixture
def qdrant_memory_record():
    return MemoryRecord(
        id="test_id1",
        text="sample text1",
        is_reference=False,
        embedding=np.array([0.5, 0.5]),
        description="description",
        external_source_name="external source",
        timestamp="timestamp",
    )


def test_qdrant_constructor():
    qdrant_mem_store = QdrantMemoryStore
    assert qdrant_mem_store._client is not None


@pytest.mark.asyncio
async def test_create_and_get_collection_async():
    qdrant_mem_store = QdrantMemoryStore
    
    await qdrant_mem_store.create_collection_async("test_collection")
    result = await qdrant_mem_store.get_collection_async("test_collection")
    assert result.name == "test_collection"


@pytest.mark.asyncio
async def test_get_collections_async():
    qdrant_mem_store = QdrantMemoryStore
    
    await qdrant_mem_store.create_collection_async("test_collection1")
    await qdrant_mem_store.create_collection_async("test_collection2")
    await qdrant_mem_store.create_collection_async("test_collection3")
    result = await qdrant_mem_store.get_collections_async()

    assert len(result) == 3


@pytest.mark.asyncio
async def test_delete_collection_async():
    qdrant_mem_store = QdrantMemoryStore
    
    await qdrant_mem_store.create_collection_async("test_collection")
    await qdrant_mem_store.delete_collection_async("test_collection")
    result = await qdrant_mem_store.get_collections_async()
    assert len(result) == 0

    await qdrant_mem_store.create_collection_async("test_collection")
    await qdrant_mem_store.delete_collection_async("test_collection")
    result = await qdrant_mem_store.get_collections_async()
    assert len(result) == 0


@pytest.mark.asyncio
async def test_does_collection_exist_async():
    qdrant_mem_store = QdrantMemoryStore    
    
    await qdrant_mem_store.create_collection_async("test_collection")
    result = await qdrant_mem_store.does_collection_exist_async("test_collection")
    assert result is True

    result = await qdrant_mem_store.does_collection_exist_async("TEST_COLLECTION")
    assert result is True



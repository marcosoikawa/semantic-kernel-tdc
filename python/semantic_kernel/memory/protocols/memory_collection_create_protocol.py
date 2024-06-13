# Copyright (c) Microsoft. All rights reserved.

from typing import Protocol, runtime_checkable


@runtime_checkable()
class MemoryCollectionCreateProtocol(Protocol):
    async def create_collection(self, collection_name: str, **kwargs):
        pass

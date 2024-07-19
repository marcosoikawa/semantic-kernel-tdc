# Copyright (c) Microsoft. All rights reserved.

import logging
import sys
from collections.abc import Sequence
from typing import Any, TypeVar

from semantic_kernel.connectors.memory.redis.utils import RedisWrapper
from semantic_kernel.data.vector_store import VectorStore
from semantic_kernel.data.vector_store_model_definition import VectorStoreRecordDefinition
from semantic_kernel.data.vector_store_record_collection import VectorStoreRecordCollection

if sys.version_info >= (3, 12):
    from typing import override  # pragma: no cover
else:
    from typing_extensions import override  # pragma: no cover

from pydantic import ValidationError
from redis.asyncio.client import Redis

from semantic_kernel.exceptions.memory_connector_exceptions import MemoryConnectorInitializationError
from semantic_kernel.utils.experimental_decorator import experimental_class

logger: logging.Logger = logging.getLogger(__name__)

TModel = TypeVar("TModel")


@experimental_class
class RedisStore(VectorStore):
    """Create a Redis Vector Store."""

    redis_database: Redis

    def __init__(
        self,
        connection_string: str | None = None,
        env_file_path: str | None = None,
        env_file_encoding: str | None = None,
        redis_database: Redis | None = None,
        **kwargs: Any,
    ) -> None:
        """RedisMemoryStore is an abstracted interface to interact with a Redis node connection.

        See documentation about connections: https://redis-py.readthedocs.io/en/stable/connections.html
        See documentation about vector attributes: https://redis.io/docs/stack/search/reference/vectors.

        """
        if redis_database:
            super().__init__(database=redis_database)
            return
        try:
            from semantic_kernel.connectors.memory.redis.redis_settings import RedisSettings

            redis_settings = RedisSettings.create(
                connection_string=connection_string,
                env_file_path=env_file_path,
                env_file_encoding=env_file_encoding,
            )
        except ValidationError as ex:
            raise MemoryConnectorInitializationError("Failed to create Redis settings.", ex) from ex
        super().__init__(redis_database=RedisWrapper.from_url(redis_settings.connection_string.get_secret_value()))

    @override
    async def list_collection_names(self, **kwargs) -> Sequence[str]:
        return [name.decode() for name in self.redis_database.execute_command("FT._LIST")]

    def get_collection(
        self,
        collection_name: str,
        data_model_type: type[TModel],
        data_model_definition: VectorStoreRecordDefinition | None = None,
        **kwargs: Any,
    ) -> "VectorStoreRecordCollection":
        """Get a QdrantCollection tied to a collection.

        Args:
            collection_name (str): The name of the collection.
            data_model_type (type[TModel]): The type of the data model.
            data_model_definition (VectorStoreRecordDefinition | None): The model fields, optional.

            **kwargs: Additional keyword arguments, passed to the collection constructor.
        """
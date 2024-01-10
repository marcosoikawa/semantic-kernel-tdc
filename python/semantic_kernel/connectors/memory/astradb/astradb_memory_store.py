# Copyright (c) Microsoft. All rights reserved.

from logging import Logger
from typing import List, Optional, Tuple

from numpy import ndarray

from semantic_kernel.connectors.memory.astradb.astra_client import AstraClient
from semantic_kernel.connectors.memory.astradb.utils import (
    build_payload,
    parse_payload,
)
from semantic_kernel.memory.memory_record import MemoryRecord
from semantic_kernel.memory.memory_store_base import MemoryStoreBase

MAX_DIMENSIONALITY = 20000
MAX_UPSERT_BATCH_SIZE = 100
MAX_QUERY_WITHOUT_METADATA_BATCH_SIZE = 10000
MAX_QUERY_WITH_METADATA_BATCH_SIZE = 1000
MAX_FETCH_BATCH_SIZE = 1000
MAX_DELETE_BATCH_SIZE = 1000


class AstraDBMemoryStore(MemoryStoreBase):
    """A memory store that uses Astra database as the backend."""

    logger: logging.Logger = logging.getLogger(__name__)

    def __init__(
        self,
        astra_application_token: str,
        astra_id: str,
        astra_region: str,
        keyspace_name: str,
        embedding_dim: int,
        similarity: str,
        logger: Optional[logging.Logger] = None,
    ) -> None:
        """Initializes a new instance of the AstraDBMemoryStore class.

        Arguments:
            astra_application_token {str} -- The Astra application token.
            astra_id {str} -- The Astra id of database.
            astra_region {str} -- The Astra region
            keyspace_name {str} -- The Astra keyspace
            embedding_dim {int} -- The dimensionality to use for new collections.
            similarity {str} -- TODO
            logger {Optional[Logger]} -- The logger to use. (default: {None})
        """
        self._embedding_dim = embedding_dim
        self._similarity = similarity

        if self._embedding_dim > MAX_DIMENSIONALITY:
            raise ValueError(
                f"Dimensionality of {self._embedding_dim} exceeds "
                + f"the maximum allowed value of {MAX_DIMENSIONALITY}."
            )

        self._client = AstraClient(
            astra_id=astra_id,
            astra_region=astra_region,
            astra_application_token=astra_application_token,
            keyspace_name=keyspace_name,
            embedding_dim=embedding_dim,
            similarity_function=similarity,
        )

    def get_collections(self) -> List[str]:
        """Gets the list of collections.

        Returns:
            List[str] -- The list of collections.
        """
        return self._client.find_collections(False)

    async def get_collections_async(self) -> List[str]:
        """Gets the list of collections.

        Returns:
            List[str] -- The list of collections.
        """
        return self.get_collections()

    async def create_collection_async(
        self,
        collection_name: str,
        dimension_num: Optional[int] = None,
        distance_type: Optional[str] = "cosine",
    ) -> None:
        """Creates a new collection in Astra if it does not exist.

        Arguments:
            collection_name {str} -- The name of the collection to create.
            dimension_num {int} -- The dimension of the vectors to be stored in this collection.
            distance_type {str} -- Specifies the similarity metric to be used when querying or comparing vectors within this collection. The available options are dot_product, euclidean, and cosine.
        Returns:
            None
        """
        dimension_num = dimension_num if dimension_num is not None else self._embedding_dim
        distance_type = distance_type if distance_type is not None else self._similarity

        if dimension_num > MAX_DIMENSIONALITY:
            raise ValueError(
                f"Dimensionality of {dimension_num} exceeds " + f"the maximum allowed value of {MAX_DIMENSIONALITY}."
            )

        result = self._client.create_collection(collection_name, dimension_num, distance_type)
        if result == True:
            self._logger.info(f"Collection {collection_name} created.")

    async def delete_collection_async(self, collection_name: str) -> None:
        """Deletes a collection.

        Arguments:
            collection_name {str} -- The name of the collection to delete.

        Returns:
            None
        """
        result = self._client.delete_collection(collection_name)
        if result == True:
            self._logger.info(f"Collection {collection_name} deleted.")
        else:
            self._logger.warning(f"Collection {collection_name} does not exist.")

    async def does_collection_exist_async(self, collection_name: str) -> bool:
        """Checks if a collection exists.

        Arguments:
            collection_name {str} -- The name of the collection to check.

        Returns:
            bool -- True if the collection exists; otherwise, False.
        """
        return self._client.find_collection(collection_name)

    async def upsert_async(self, collection_name: str, record: MemoryRecord) -> str:
        """Upserts a memory record into the data store. Does not guarantee that the collection exists.
            If the record already exists, it will be updated.
            If the record does not exist, it will be created.

        Arguments:
            collection_name {str} -- The name associated with a collection of embeddings.
            record {MemoryRecord} -- The memory record to upsert.

        Returns:
            str -- The unique identifier for the memory record.
        """
        filter = {"_id": record._id}
        update = {"$set": build_payload(record)}
        status = self._client.update_document(collection_name, filter, update, True)

        return status["upsertedId"] if "upsertedId" in status else record._id

    async def upsert_batch_async(self, collection_name: str, records: List[MemoryRecord]) -> List[str]:
        upserted_ids = []

        for record in records:
            id = await self.upsert_async(collection_name, record)
            upserted_ids.append(id)

        return upserted_ids

    async def get_async(self, collection_name: str, key: str, with_embedding: bool = False) -> MemoryRecord:
        """Gets a record. Does not guarantee that the collection exists.

        Arguments:
            collection_name {str} -- The name of the collection to get the record from.
            key {str} -- The unique database key of the record.
            with_embedding {bool} -- Whether to include the embedding in the result. (default: {False})

        Returns:
            MemoryRecord -- The record.
        """
        filter = {"_id": key}
        documents = self._client.find_documents(
            collection_name=collection_name, filter=filter, include_vector=with_embedding
        )

        if len(documents) == 0:
            raise KeyError(f"Record with key '{key}' does not exist")

        return parse_payload(documents[0])

    async def get_batch_async(
        self, collection_name: str, keys: List[str], with_embeddings: bool = False
    ) -> List[MemoryRecord]:
        """Gets a batch of records. Does not guarantee that the collection exists.

        Arguments:
            collection_name {str} -- The name of the collection to get the records from.
            keys {List[str]} -- The unique database keys of the records.
            with_embeddings {bool} -- Whether to include the embeddings in the results. (default: {False})

        Returns:
            List[MemoryRecord] -- The records.
        """

        filter = {"_id": {"$in": keys}}
        documents = self._client.find_documents(
            collection_name=collection_name, filter=filter, include_vector=with_embeddings
        )
        return [parse_payload(document) for document in documents]

    async def remove_async(self, collection_name: str, key: str) -> None:
        """Removes a memory record from the data store. Does not guarantee that the collection exists.

        Arguments:
            collection_name {str} -- The name of the collection to remove the record from.
            key {str} -- The unique id associated with the memory record to remove.

        Returns:
            None
        """
        filter = {"_id": key}
        self._client.delete_documents(collection_name, filter)

    async def remove_batch_async(self, collection_name: str, keys: List[str]) -> None:
        """Removes a batch of records. Does not guarantee that the collection exists.

        Arguments:
            collection_name {str} -- The name of the collection to remove the records from.
            keys {List[str]} -- The unique ids associated with the memory records to remove.

        Returns:
            None
        """
        filter = {"_id": {"$in": keys}}
        self._client.delete_documents(collection_name, filter)

    async def get_nearest_match_async(
        self,
        collection_name: str,
        embedding: ndarray,
        min_relevance_score: float = 0.0,
        with_embedding: bool = False,
    ) -> Tuple[MemoryRecord, float]:
        """Gets the nearest match to an embedding using cosine similarity.
        Arguments:
            collection_name {str} -- The name of the collection to get the nearest matches from.
            embedding {ndarray} -- The embedding to find the nearest matches to.
            min_relevance_score {float} -- The minimum relevance score of the matches. (default: {0.0})
            with_embeddings {bool} -- Whether to include the embeddings in the results. (default: {False})

        Returns:
            List[Tuple[MemoryRecord, float]] -- The records and their relevance scores.
        """
        matches = await self.get_nearest_matches_async(
            collection_name=collection_name,
            embedding=embedding,
            limit=1,
            min_relevance_score=min_relevance_score,
            with_embeddings=with_embedding,
        )
        return matches[0]

    async def get_nearest_matches_async(
        self,
        collection_name: str,
        embedding: ndarray,
        limit: int,
        min_relevance_score: float = 0.0,
        with_embeddings: bool = False,
    ) -> List[Tuple[MemoryRecord, float]]:
        """Gets the nearest matches to an embedding using cosine similarity.
        Arguments:
            collection_name {str} -- The name of the collection to get the nearest matches from.
            embedding {ndarray} -- The embedding to find the nearest matches to.
            limit {int} -- The maximum number of matches to return.
            min_relevance_score {float} -- The minimum relevance score of the matches. (default: {0.0})
            with_embeddings {bool} -- Whether to include the embeddings in the results. (default: {False})

        Returns:
            List[Tuple[MemoryRecord, float]] -- The records and their relevance scores.
        """
        matches = self._client.find_documents(
            collection_name=collection_name,
            vector=embedding.tolist(),
            limit=limit,
            include_similarity=True,
            include_vector=with_embeddings,
        )

        if min_relevance_score:
            matches = [match for match in matches if match["$similarity"] >= min_relevance_score]

        return (
            [
                (
                    parse_payload(match),
                    match["$similarity"],
                )
                for match in matches
            ]
            if len(matches) > 0
            else []
        )

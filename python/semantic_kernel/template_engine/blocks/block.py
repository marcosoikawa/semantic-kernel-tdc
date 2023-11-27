# Copyright (c) Microsoft. All rights reserved.

from logging import Logger
from typing import Optional, Tuple

from pydantic import PrivateAttr

from semantic_kernel.sk_pydantic import SKBaseModel
from semantic_kernel.template_engine.blocks.block_types import BlockTypes
from semantic_kernel.utils.null_logger import NullLogger


class Block(SKBaseModel):
    content: Optional[str] = None
    _log: Optional[Logger] = PrivateAttr(default_factory=NullLogger)
    
    def __init__(
        self, content: Optional[str] = None, log: Optional[Logger] = None
    ) -> None:
        super().__init__(content=content)
        self._log = log or NullLogger()

    def is_valid(self) -> Tuple[bool, str]:
        raise NotImplementedError("Subclasses must implement this method.")

    @property
    def type(self) -> BlockTypes:
        return BlockTypes.UNDEFINED

    @property
    def log(self) -> Logger:
        return self._log

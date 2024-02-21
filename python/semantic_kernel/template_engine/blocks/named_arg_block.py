# Copyright (c) Microsoft. All rights reserved.

import logging
from re import compile
from typing import TYPE_CHECKING, Any, ClassVar, Optional

from pydantic import model_validator

from semantic_kernel.template_engine.blocks.block import Block
from semantic_kernel.template_engine.blocks.block_errors import NamedArgBlockSyntaxError
from semantic_kernel.template_engine.blocks.block_types import BlockTypes
from semantic_kernel.template_engine.blocks.val_block import ValBlock
from semantic_kernel.template_engine.blocks.var_block import VarBlock

if TYPE_CHECKING:
    from semantic_kernel.functions.kernel_arguments import KernelArguments
    from semantic_kernel.kernel import Kernel

logger: logging.Logger = logging.getLogger(__name__)

NAMED_ARG_REGEX = r"^(?P<name>[0-9A-Za-z_]+)[=]{1}(?P<value>[${1}](?P<var_name>[0-9A-Za-z_]+)|(?P<quote>[\"'])(?P<val>.[^\"^']*)(?P=quote))$"

NAMED_ARG_MATCHER = compile(NAMED_ARG_REGEX)


class NamedArgBlock(Block):
    type: ClassVar[BlockTypes] = BlockTypes.NAMED_ARG
    name: Optional[str] = None
    value: Optional[ValBlock] = None
    variable: Optional[VarBlock] = None

    @model_validator(mode="before")
    @classmethod
    def parse_content(cls, fields: Any) -> Any:
        if isinstance(fields, Block) or ("name" in fields and "value" in fields):
            return fields
        content = fields.get("content", "").strip()
        matches = NAMED_ARG_MATCHER.match(content)
        if not matches:
            raise NamedArgBlockSyntaxError(content=content)
        matches_dict = matches.groupdict()
        if name := matches_dict.get("name"):
            fields["name"] = name
        if value := matches_dict.get("value"):
            if matches_dict.get("var_name"):
                fields["variable"] = VarBlock(content=value, name=matches_dict["var_name"])
            elif matches_dict.get("val"):
                fields["value"] = ValBlock(content=value, value=matches_dict["val"], quote=matches_dict["quote"])
        return fields

    def render(self, kernel: "Kernel", arguments: Optional["KernelArguments"] = None) -> Any:
        if self.value:
            return self.value.render(kernel, arguments)
        if arguments is None:
            return ""
        if self.variable:
            return self.variable.render(kernel, arguments)

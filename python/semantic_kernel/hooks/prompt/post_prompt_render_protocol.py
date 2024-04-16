# Copyright (c) Microsoft. All rights reserved.
from __future__ import annotations

from typing import TYPE_CHECKING, Protocol, runtime_checkable

if TYPE_CHECKING:
    from semantic_kernel.hooks.prompt import PostPromptRenderContext


@runtime_checkable
class PostPromptRenderProtocol(Protocol):
    """PostPromptRenderProtocol."""

    def post_prompt_render(self, context: "PostPromptRenderContext") -> None:
        """post_prompt_render."""

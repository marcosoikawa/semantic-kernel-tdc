# Copyright (c) Microsoft. All rights reserved.

from typing import Optional, Protocol

from semantic_kernel.orchestration.context_variables import ContextVariables


class TextRenderer(Protocol):
    """
    Protocol for static (text) blocks that don't need async rendering.
    """

    def render(self, variables: Optional[ContextVariables] = None) -> str:
        """
        Render the block using only the given variables.

        :param variables: Optional variables used to render the block
        :return: Rendered content
        """
        ...

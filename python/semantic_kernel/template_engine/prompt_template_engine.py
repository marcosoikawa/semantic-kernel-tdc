# Copyright (c) Microsoft. All rights reserved.

import logging
from typing import TYPE_CHECKING, List, Optional

from pydantic import PrivateAttr

from semantic_kernel.kernel_pydantic import KernelBaseModel
from semantic_kernel.template_engine.blocks.block import Block
from semantic_kernel.template_engine.blocks.block_types import BlockTypes
from semantic_kernel.template_engine.protocols.text_renderer import TextRenderer
from semantic_kernel.template_engine.template_tokenizer import TemplateTokenizer

if TYPE_CHECKING:
    from semantic_kernel.orchestration.kernel_arguments import KernelArguments

logger: logging.Logger = logging.getLogger(__name__)


class PromptTemplateEngine(KernelBaseModel):
    _tokenizer: TemplateTokenizer = PrivateAttr()

    def __init__(self, **kwargs) -> None:
        super().__init__()

        if kwargs.get("logger"):
            logger.warning("The `logger` parameter is deprecated. Please use the `logging` module instead.")
        self._tokenizer = TemplateTokenizer()

    def extract_blocks(self, template_text: Optional[str] = None, validate: bool = True) -> List[Block]:
        """
        Given a prompt template string, extract all the blocks
        (text, variables, function calls).

        :param template_text: Prompt template (see skprompt.txt files)
        :param validate: Whether to validate the blocks syntax, or just
            return the blocks found, which could contain invalid code
        :return: A list of all the blocks, ie the template tokenized in
            text, variables and function calls
        """
        logger.debug(f"Extracting blocks from template: {template_text}")
        blocks = self._tokenizer.tokenize(template_text)

        if validate:
            for block in blocks:
                is_valid, error_message = block.is_valid()
                if not is_valid:
                    raise ValueError(error_message)

        return blocks

    async def render(self, template_text: str, arguments: "KernelArguments") -> str:
        """
        Given a prompt template, replace the variables with their values
        and execute the functions replacing their reference with the
        function result.

        :param template_text: Prompt template (see skprompt.txt files)
        :param context: Access into the current kernel execution context
        :return: The prompt template ready to be used for an AI request
        """
        logger.debug(f"Rendering string template: {template_text}")
        blocks = self.extract_blocks(template_text)
        return await self.render_blocks(blocks, arguments)

    async def render_blocks(self, blocks: List[Block], arguments: "KernelArguments") -> str:
        """
        Given a list of blocks render each block and compose the final result.

        :param blocks: Template blocks generated by ExtractBlocks
        :param context: Access into the current kernel execution context
        :return: The prompt template ready to be used for an AI request
        """
        from semantic_kernel.template_engine.protocols.code_renderer import CodeRenderer

        logger.debug(f"Rendering list of {len(blocks)} blocks")
        rendered_blocks = []
        for block in blocks:
            if isinstance(block, TextRenderer):
                rendered_blocks.append(block.render(arguments))
            elif isinstance(block, CodeRenderer):
                rendered_blocks.append(await block.render_code(arguments))
            else:
                error = "unexpected block type, the block doesn't have a rendering " "protocol assigned to it"
                logger.error(error)
                raise ValueError(error)

        logger.debug(f"Rendered prompt: {''.join(rendered_blocks)}")
        return "".join(rendered_blocks)

    def render_variables(self, blocks: List[Block], arguments: Optional["KernelArguments"] = None) -> List[Block]:
        """
        Given a list of blocks, render the Variable Blocks, replacing
        placeholders with the actual value in memory.

        :param blocks: List of blocks, typically all the blocks found in a template
        :param variables: Container of all the temporary variables known to the kernel
        :return: An updated list of blocks where Variable Blocks have rendered to
            Text Blocks
        """
        from semantic_kernel.template_engine.blocks.text_block import TextBlock

        logger.debug("Rendering variables")

        rendered_blocks = []
        for block in blocks:
            if block.type != BlockTypes.VARIABLE:
                rendered_blocks.append(block)
                continue
            if not isinstance(block, TextRenderer):
                raise ValueError("TextBlock must implement TextRenderer protocol")
            rendered_blocks.append(TextBlock.from_text(block.render(arguments)))

        return rendered_blocks

    async def render_code(self, blocks: List[Block], arguments: "KernelArguments") -> List[Block]:
        """
        Given a list of blocks, render the Code Blocks, executing the
        functions and replacing placeholders with the functions result.

        :param blocks: List of blocks, typically all the blocks found in a template
        :param execution_context: Access into the current kernel execution context
        :return: An updated list of blocks where Code Blocks have rendered to
            Text Blocks
        """
        from semantic_kernel.template_engine.blocks.text_block import TextBlock
        from semantic_kernel.template_engine.protocols.code_renderer import CodeRenderer

        logger.debug("Rendering code")

        rendered_blocks = []
        for block in blocks:
            if block.type != BlockTypes.CODE:
                rendered_blocks.append(block)
                continue
            if not isinstance(block, CodeRenderer):
                raise ValueError("CodeBlock must implement CodeRenderer protocol")
            rendered_blocks.append(TextBlock.from_text(await block.render_code(arguments)))

        return rendered_blocks

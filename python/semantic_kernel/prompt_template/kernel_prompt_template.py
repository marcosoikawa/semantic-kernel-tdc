# Copyright (c) Microsoft. All rights reserved.

import logging
from typing import TYPE_CHECKING, Any, List, Optional

from pydantic import PrivateAttr

from semantic_kernel.functions.kernel_arguments import KernelArguments
from semantic_kernel.prompt_template.input_variable import InputVariable
from semantic_kernel.prompt_template.prompt_template_base import PromptTemplateBase
from semantic_kernel.prompt_template.prompt_template_config import PromptTemplateConfig
from semantic_kernel.template_engine.blocks.block import Block
from semantic_kernel.template_engine.blocks.block_types import BlockTypes
from semantic_kernel.template_engine.protocols.text_renderer import TextRenderer
from semantic_kernel.template_engine.template_tokenizer import TemplateTokenizer

if TYPE_CHECKING:
    from semantic_kernel.kernel import Kernel

logger: logging.Logger = logging.getLogger(__name__)


class KernelPromptTemplate(PromptTemplateBase):
    prompt_template_config: PromptTemplateConfig
    _blocks: List[Block] = PrivateAttr(default_factory=list)

    def model_post_init(self, __context: Any) -> None:
        self._blocks = self.extract_blocks()
        # Add all of the existing input variables to our known set. We'll avoid adding any
        # dynamically discovered input variables with the same name.
        seen = {iv.name.lower() for iv in self.prompt_template_config.input_variables}

        # Enumerate every block in the template, adding any variables that are referenced.
        for block in self._blocks:
            if block.type == BlockTypes.VARIABLE:
                # Add all variables from variable blocks, e.g. "{{$a}}".
                self._add_if_missing(block.name, seen)
                continue
            if block.type == BlockTypes.CODE:
                for sub_block in block.tokens:
                    if sub_block.type == BlockTypes.VARIABLE:
                        # Add all variables from code blocks, e.g. "{{p.bar $b}}".
                        self._add_if_missing(sub_block.name, seen)
                        continue
                    if sub_block.type == BlockTypes.NAMED_ARG and sub_block.variable:
                        # Add all variables from named arguments, e.g. "{{p.bar b = $b}}".
                        # represents a named argument for a function call.
                        # For example, in the template {{ MyPlugin.MyFunction var1=$boo }}, var1=$boo
                        # is a named arg block.
                        self._add_if_missing(sub_block.variable.name, seen)

    def _add_if_missing(self, variable_name: str, seen: Optional[set] = None):
        # Convert variable_name to lower case to handle case-insensitivity
        if variable_name and variable_name.lower() not in seen:
            seen.add(variable_name.lower())
            self.prompt_template_config.input_variables.append(InputVariable(name=variable_name))

    def extract_blocks(self) -> List[Block]:
        """
        Given a prompt template string, extract all the blocks
        (text, variables, function calls).

        Args:
            template_text: Prompt template

        Returns:
            A list of all the blocks, ie the template tokenized in
            text, variables and function calls
        """
        logger.debug(f"Extracting blocks from template: {self.prompt_template_config.template}")
        if not self.prompt_template_config.template:
            return []
        return TemplateTokenizer.tokenize(self.prompt_template_config.template)

    async def render(self, kernel: "Kernel", arguments: Optional["KernelArguments"] = None) -> str:
        """
        Using the prompt template, replace the variables with their values
        and execute the functions replacing their reference with the
        function result.

        Args:
            kernel: The kernel instance
            arguments: The kernel arguments

        Returns:
            The prompt template ready to be used for an AI request
        """
        if not arguments:
            arguments = KernelArguments()
        return await self.render_blocks(self._blocks, kernel, arguments)

    async def render_blocks(self, blocks: List[Block], kernel: "Kernel", arguments: "KernelArguments") -> str:
        """
        Given a list of blocks render each block and compose the final result.

        :param blocks: Template blocks generated by ExtractBlocks
        :param context: Access into the current kernel execution context
        :return: The prompt template ready to be used for an AI request
        """
        from semantic_kernel.template_engine.protocols.code_renderer import CodeRenderer

        logger.debug(f"Rendering list of {len(blocks)} blocks")
        rendered_blocks: List[str] = []
        for block in blocks:
            if isinstance(block, TextRenderer):
                rendered_blocks.append(block.render(kernel, arguments))
                continue
            if isinstance(block, CodeRenderer):
                rendered_blocks.append(await block.render_code(kernel, arguments))
        prompt = "".join(rendered_blocks)
        logger.debug(f"Rendered prompt: {prompt}")
        return prompt

    def render_variables(
        self, blocks: List[Block], kernel: "Kernel", arguments: Optional["KernelArguments"] = None
    ) -> List[Block]:
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

        rendered_blocks: List[Block] = []
        for block in blocks:
            if block.type == BlockTypes.VARIABLE:
                rendered_blocks.append(TextBlock.from_text(block.render(kernel, arguments)))
                continue
            rendered_blocks.append(block)

        return rendered_blocks

    async def render_code(self, blocks: List[Block], kernel: "Kernel", arguments: "KernelArguments") -> List[Block]:
        """
        Given a list of blocks, render the Code Blocks, executing the
        functions and replacing placeholders with the functions result.

        :param blocks: List of blocks, typically all the blocks found in a template
        :param execution_context: Access into the current kernel execution context
        :return: An updated list of blocks where Code Blocks have rendered to
            Text Blocks
        """
        from semantic_kernel.template_engine.blocks.text_block import TextBlock

        logger.debug("Rendering code")

        rendered_blocks: List[Block] = []
        for block in blocks:
            if block.type == BlockTypes.CODE:
                rendered_blocks.append(TextBlock.from_text(await block.render_code(kernel, arguments)))
                continue
            rendered_blocks.append(block)

        return rendered_blocks

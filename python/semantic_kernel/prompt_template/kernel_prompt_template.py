# Copyright (c) Microsoft. All rights reserved.

import logging
from html import escape
from typing import TYPE_CHECKING, Any, Optional

from pydantic import PrivateAttr, field_validator

from semantic_kernel.exceptions import TemplateRenderException
from semantic_kernel.functions.kernel_arguments import KernelArguments
from semantic_kernel.prompt_template.const import KERNEL_TEMPLATE_FORMAT_NAME
from semantic_kernel.prompt_template.input_variable import InputVariable
from semantic_kernel.prompt_template.prompt_template_base import PromptTemplateBase
from semantic_kernel.template_engine.blocks.block import Block
from semantic_kernel.template_engine.blocks.block_types import BlockTypes
from semantic_kernel.template_engine.template_tokenizer import TemplateTokenizer

if TYPE_CHECKING:
    from semantic_kernel.kernel import Kernel
    from semantic_kernel.prompt_template.prompt_template_config import PromptTemplateConfig

logger: logging.Logger = logging.getLogger(__name__)


class KernelPromptTemplate(PromptTemplateBase):
    """Create a Kernel prompt template.

    Args:
        prompt_template_config (PromptTemplateConfig): The prompt template configuration
            This includes the actual template to use.
        allow_dangerously_set_content (bool = False): Allow content without encoding throughout, this overrides
            the same settings in the prompt template config and input variables.
            This reverts the behavior to unencoded input.

    Raises:
        ValueError: If the template format is not 'semantic-kernel'
        TemplateSyntaxError: If the template has a syntax error
    """

    _blocks: list[Block] = PrivateAttr(default_factory=list)

    @field_validator("prompt_template_config")
    @classmethod
    def validate_template_format(cls, v: "PromptTemplateConfig") -> "PromptTemplateConfig":
        """Validate the template format."""
        if v.template_format != KERNEL_TEMPLATE_FORMAT_NAME:
            raise ValueError(f"Invalid prompt template format: {v.template_format}. Expected: semantic-kernel")
        return v

    def model_post_init(self, __context: Any) -> None:
        """Post init model."""
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

    def _add_if_missing(self, variable_name: str, seen: set | None = None):
        # Convert variable_name to lower case to handle case-insensitivity
        if variable_name and variable_name.lower() not in seen:
            seen.add(variable_name.lower())
            self.prompt_template_config.input_variables.append(InputVariable(name=variable_name))

    def extract_blocks(self) -> list[Block]:
        """Given the prompt template, extract all the blocks (text, variables, function calls).

        Returns:
            A list of all the blocks, ie the template tokenized in
            text, variables and function calls
        """
        logger.debug(f"Extracting blocks from template: {self.prompt_template_config.template}")
        if not self.prompt_template_config.template:
            return []
        return TemplateTokenizer.tokenize(self.prompt_template_config.template)

    async def render(self, kernel: "Kernel", arguments: Optional["KernelArguments"] = None) -> str:
        """Render the prompt template.

        Using the prompt template, replace the variables with their values
        and execute the functions replacing their reference with the
        function result.

        Args:
            kernel: The kernel instance
            arguments: The kernel arguments

        Returns:
            The prompt template ready to be used for an AI request
        """
        if arguments is None:
            arguments = KernelArguments()
        return await self.render_blocks(self._blocks, kernel, arguments)

    async def render_blocks(self, blocks: list[Block], kernel: "Kernel", arguments: "KernelArguments") -> str:
        """Given a list of blocks render each block and compose the final result.

        :param blocks: Template blocks generated by ExtractBlocks
        :param context: Access into the current kernel execution context
        :return: The prompt template ready to be used for an AI request
        """
        from semantic_kernel.template_engine.protocols.code_renderer import CodeRenderer
        from semantic_kernel.template_engine.protocols.text_renderer import TextRenderer

        logger.debug(f"Rendering list of {len(blocks)} blocks")
        rendered_blocks: list[str] = []

        arguments = self._get_trusted_arguments(arguments)
        allow_unsafe_function_output = self._get_allow_unsafe_function_output()
        for block in blocks:
            if isinstance(block, TextRenderer):
                rendered_blocks.append(block.render(kernel, arguments))
                continue
            if isinstance(block, CodeRenderer):
                try:
                    rendered = await block.render_code(kernel, arguments)
                except Exception as exc:
                    logger.error(f"Error rendering code block: {exc}")
                    raise TemplateRenderException(f"Error rendering code block: {exc}") from exc
                rendered_blocks.append(rendered if allow_unsafe_function_output else escape(rendered))
        prompt = "".join(rendered_blocks)
        logger.debug(f"Rendered prompt: {prompt}")
        return prompt

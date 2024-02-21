# Copyright (c) Microsoft. All rights reserved.

import logging
from typing import List

from semantic_kernel.template_engine.blocks.block import Block
from semantic_kernel.template_engine.blocks.block_errors import (
    CodeBlockSyntaxError,
    CodeBlockTokenError,
    FunctionIdBlockSyntaxError,
    TemplateSyntaxError,
    ValBlockSyntaxError,
    VarBlockSyntaxError,
)
from semantic_kernel.template_engine.blocks.block_types import BlockTypes
from semantic_kernel.template_engine.blocks.code_block import CodeBlock
from semantic_kernel.template_engine.blocks.symbols import Symbols
from semantic_kernel.template_engine.blocks.text_block import TextBlock
from semantic_kernel.template_engine.code_tokenizer import CodeTokenizer

logger: logging.Logger = logging.getLogger(__name__)


# BNF parsed by TemplateTokenizer:
# [template]       ::= "" | [block] | [block] [template]
# [block]          ::= [sk-block] | [text-block]
# [sk-block]       ::= "{{" [variable] "}}"
#                      | "{{" [value] "}}"
#                      | "{{" [function-call] "}}"
# [text-block]     ::= [any-char] | [any-char] [text-block]
# [any-char]       ::= any char
class TemplateTokenizer:
    @staticmethod
    def tokenize(text: str) -> List[Block]:
        code_tokenizer = CodeTokenizer()
        # An empty block consists of 4 chars: "{{}}"
        EMPTY_CODE_BLOCK_LENGTH = 4
        # A block shorter than 5 chars is either empty or
        # invalid, e.g. "{{ }}" and "{{$}}"
        MIN_CODE_BLOCK_LENGTH = EMPTY_CODE_BLOCK_LENGTH + 1

        text = text or ""

        # Render None/empty to ""
        if not text:
            return [TextBlock.from_text("")]

        # If the template is "empty" return it as a text block
        if len(text) < MIN_CODE_BLOCK_LENGTH:
            return [TextBlock.from_text(text)]

        blocks: List[Block] = []
        end_of_last_block = 0
        block_start_pos = 0
        block_start_found = False
        inside_text_value = False
        text_value_delimiter = None
        skip_next_char = False

        # for next_char_cursor in range(1, len(text)):
        for current_char_pos, current_char in enumerate(text[:-1]):
            next_char_pos = current_char_pos + 1
            next_char = text[next_char_pos]

            if skip_next_char:
                skip_next_char = False
                continue

            # When "{{" is found outside a value
            # Note: "{{ {{x}}" => ["{{ ", "{{x}}"]
            if not inside_text_value and current_char == Symbols.BLOCK_STARTER and next_char == Symbols.BLOCK_STARTER:
                # A block starts at the first "{"
                block_start_pos = current_char_pos
                block_start_found = True

            # After having found "{{"
            if block_start_found:
                # While inside a text value, when the end quote is found
                if inside_text_value:
                    # If the current char is escaping the next special char we skip
                    if current_char == Symbols.ESCAPE_CHAR and next_char in (
                        Symbols.DBL_QUOTE,
                        Symbols.SGL_QUOTE,
                        Symbols.ESCAPE_CHAR,
                    ):
                        skip_next_char = True
                        continue

                    if current_char == text_value_delimiter:
                        inside_text_value = False
                else:
                    # A value starts here
                    if current_char in (Symbols.DBL_QUOTE, Symbols.SGL_QUOTE):
                        inside_text_value = True
                        text_value_delimiter = current_char
                    # If the block ends here
                    elif current_char == Symbols.BLOCK_ENDER and next_char == Symbols.BLOCK_ENDER:
                        # If there is plain text before the current
                        # var/val/code block and the previous one,
                        # add it as a text block
                        if block_start_pos > end_of_last_block:
                            blocks.append(
                                TextBlock.from_text(
                                    text,
                                    end_of_last_block,
                                    block_start_pos,
                                )
                            )

                        # Extract raw block
                        content_with_delimiters = text[block_start_pos : next_char_pos + 1]  # noqa: E203
                        # Remove "{{" and "}}" delimiters and trim whitespace
                        content_without_delimiters = content_with_delimiters[2:-2].strip()

                        if len(content_without_delimiters) == 0:
                            # If what is left is empty, consider the raw block
                            # a TextBlock
                            blocks.append(TextBlock.from_text(content_with_delimiters))
                        else:
                            try:
                                code_blocks = code_tokenizer.tokenize(content_without_delimiters)
                                if code_blocks[0].type in (
                                    BlockTypes.VALUE,
                                    BlockTypes.VARIABLE,
                                    BlockTypes.TEXT,
                                ):
                                    blocks.append(code_blocks[0])
                                else:
                                    blocks.append(CodeBlock(content=content_without_delimiters, tokens=code_blocks))
                            except (
                                CodeBlockTokenError,
                                CodeBlockSyntaxError,
                                VarBlockSyntaxError,
                                ValBlockSyntaxError,
                                FunctionIdBlockSyntaxError,
                            ) as e:
                                msg = f"Failed to tokenize code block: {content_without_delimiters}. {e}"
                                logger.warning(msg)
                                raise TemplateSyntaxError(msg) from e

                        end_of_last_block = next_char_pos + 1
                        block_start_found = False

        # If there is something left after the last block, capture it as a TextBlock
        if end_of_last_block < len(text):
            blocks.append(TextBlock.from_text(text, end_of_last_block, len(text)))

        return blocks

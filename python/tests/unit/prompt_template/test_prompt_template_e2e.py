# Copyright (c) Microsoft. All rights reserved.

import os
from typing import List, Optional, Tuple

from pytest import mark, raises

from semantic_kernel import Kernel
from semantic_kernel.exceptions import TemplateSyntaxError
from semantic_kernel.functions import kernel_function
from semantic_kernel.functions.kernel_arguments import KernelArguments
from semantic_kernel.prompt_template.kernel_prompt_template import KernelPromptTemplate
from semantic_kernel.prompt_template.prompt_template_config import PromptTemplateConfig


def _get_template_language_tests(safe: bool = True) -> List[Tuple[str, str]]:
    path = __file__
    path = os.path.dirname(path)

    with open(os.path.join(path, "semantic-kernel-tests.txt"), "r") as file:
        content = file.readlines()

    key = ""
    test_data = []
    for raw_line in content:
        value = raw_line.strip()
        if not value or value.startswith("#"):
            continue

        if not key:
            key = raw_line
        else:
            if "," in raw_line:
                raw_line = (raw_line.split(",")[0 if safe else 1].strip()) + "\n"

            test_data.append((key, raw_line))
            key = ""

    return test_data


class MyPlugin:
    @kernel_function
    def check123(self, input: str) -> str:
        return "123 ok" if input == "123" else f"{input} != 123"

    @kernel_function
    def asis(self, input: Optional[str] = None) -> str:
        return input or ""


class TestPromptTemplateEngine:
    @mark.asyncio
    async def test_it_supports_variables(self, kernel: Kernel):
        # Arrange
        input = "template tests"
        winner = "SK"
        template = "And the winner\n of {{$input}} \nis: {{  $winner }}!"

        arguments = KernelArguments(input=input, winner=winner)
        # Act
        result = await KernelPromptTemplate(
            prompt_template_config=PromptTemplateConfig(name="test", description="test", template=template),
            allow_unsafe_content=True,
        ).render(kernel, arguments)
        # Assert
        expected = template.replace("{{$input}}", input).replace("{{  $winner }}", winner)
        assert expected == result

    @mark.asyncio
    async def test_it_supports_values(self, kernel: Kernel):
        # Arrange
        template = "And the winner\n of {{'template\ntests'}} \nis: {{  \"SK\" }}!"
        expected = "And the winner\n of template\ntests \nis: SK!"

        # Act
        result = await KernelPromptTemplate(
            prompt_template_config=PromptTemplateConfig(
                name="test", description="test", template=template, allow_unsafe_content=True
            )
        ).render(kernel, None)

        # Assert
        assert expected == result

    @mark.asyncio
    async def test_it_allows_to_pass_variables_to_functions(self, kernel: Kernel):
        # Arrange
        template = "== {{my.check123 $call}} =="
        kernel.add_plugin(MyPlugin(), "my")

        arguments = KernelArguments(call="123")
        # Act
        result = await KernelPromptTemplate(
            prompt_template_config=PromptTemplateConfig(
                name="test", description="test", template=template, allow_unsafe_content=True
            )
        ).render(kernel, arguments)

        # Assert
        assert "== 123 ok ==" == result

    @mark.asyncio
    async def test_it_allows_to_pass_values_to_functions(self, kernel: Kernel):
        # Arrange
        template = "== {{my.check123 '234'}} =="
        kernel.add_plugin(MyPlugin(), "my")

        # Act
        result = await KernelPromptTemplate(
            prompt_template_config=PromptTemplateConfig(
                name="test", description="test", template=template, allow_unsafe_content=True
            )
        ).render(kernel, None)

        # Assert
        assert "== 234 != 123 ==" == result

    @mark.asyncio
    async def test_it_allows_to_pass_escaped_values1_to_functions(self, kernel: Kernel):
        # Arrange
        template = "== {{my.check123 'a\\'b'}} =="
        kernel.add_plugin(MyPlugin(), "my")
        # Act
        result = await KernelPromptTemplate(
            prompt_template_config=PromptTemplateConfig(
                name="test", description="test", template=template, allow_unsafe_content=True
            )
        ).render(kernel, None)

        # Assert
        assert "== a'b != 123 ==" == result

    @mark.asyncio
    async def test_it_allows_to_pass_escaped_values2_to_functions(self, kernel: Kernel):
        # Arrange
        template = '== {{my.check123 "a\\"b"}} =='
        kernel.add_plugin(MyPlugin(), "my")

        # Act
        result = await KernelPromptTemplate(
            prompt_template_config=PromptTemplateConfig(
                name="test", description="test", template=template, allow_unsafe_content=True
            )
        ).render(kernel, None)

        # Assert
        assert '== a"b != 123 ==' == result

    @mark.asyncio
    @mark.parametrize("template,expected_result", [(t, r) for t, r in _get_template_language_tests(safe=False)])
    async def test_it_handle_edge_cases_unsafe(self, kernel: Kernel, template: str, expected_result: str):
        # Arrange
        kernel.add_plugin(MyPlugin(), "my_plugin")

        # Act
        if expected_result.startswith("ERROR"):
            with raises(TemplateSyntaxError):
                await KernelPromptTemplate(
                    prompt_template_config=PromptTemplateConfig(name="test", description="test", template=template),
                    allow_unsafe_content=True,
                ).render(kernel, KernelArguments())
        else:
            result = await KernelPromptTemplate(
                prompt_template_config=PromptTemplateConfig(name="test", description="test", template=template),
                allow_unsafe_content=True,
            ).render(kernel, KernelArguments())

            # Assert
            assert expected_result == result

    @mark.asyncio
    @mark.parametrize("template,expected_result", [(t, r) for t, r in _get_template_language_tests(safe=True)])
    async def test_it_handle_edge_cases_safe(self, kernel: Kernel, template: str, expected_result: str):
        # Arrange
        kernel.add_plugin(MyPlugin(), "my_plugin")

        # Act
        if expected_result.startswith("ERROR"):
            with raises(TemplateSyntaxError):
                await KernelPromptTemplate(
                    prompt_template_config=PromptTemplateConfig(
                        name="test",
                        description="test",
                        template=template,
                    )
                ).render(kernel, KernelArguments())
        else:
            result = await KernelPromptTemplate(
                prompt_template_config=PromptTemplateConfig(
                    name="test",
                    description="test",
                    template=template,
                )
            ).render(kernel, KernelArguments())

            # Assert
            assert expected_result == result

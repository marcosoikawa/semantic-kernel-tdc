# Copyright (c) Microsoft. All rights reserved.

import asyncio
import logging
from typing import TYPE_CHECKING, Callable

import nest_asyncio

if TYPE_CHECKING:
    from semantic_kernel.functions.kernel_arguments import KernelArguments
    from semantic_kernel.functions.kernel_function import KernelFunction
    from semantic_kernel.kernel import Kernel


logger: logging.Logger = logging.getLogger(__name__)


def create_handlebars_helper_from_function(
    function: "KernelFunction", kernel: "Kernel", base_arguments: "KernelArguments"
) -> Callable:
    """Create a helper function for the Handlebars templating engine from a kernel function."""
    if not getattr(asyncio, "_nest_patched", False):
        nest_asyncio.apply()

    def func(this, *args, **kwargs):
        arguments = base_arguments.copy()
        arguments.update(kwargs)

        logger.debug(
            f"Invoking function {function.metadata.fully_qualified_name} "
            f"with args: {args} and kwargs: {kwargs} and this: {this}."
        )
        return asyncio.run(function.invoke(kernel=kernel, arguments=arguments))

    return func

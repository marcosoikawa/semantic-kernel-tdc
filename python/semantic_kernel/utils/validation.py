# Copyright (c) Microsoft. All rights reserved.

from re import match as re_match
from typing import Optional

# Validation regexes
PLUGIN_NAME_REGEX = r"^[0-9A-Za-z_]*$"
FUNCTION_NAME_REGEX = r"^[0-9A-Za-z_]*$"
FUNCTION_PARAM_NAME_REGEX = r"^[0-9A-Za-z_]*$"


def validate_plugin_name(value: Optional[str]) -> None:
    """
    Validates that the plugin name is valid.

    Valid plugin names are non-empty and
    match the regex: [0-9A-Za-z_]*

    :param value: The plugin name to validate.

    :raises ValueError: If the plugin name is invalid.
    """
    if not value:
        raise ValueError("The plugin name cannot be `None` or empty")

    if not re_match(PLUGIN_NAME_REGEX, value):
        raise ValueError(
            f"Invalid plugin name: {value}. Plugin " f"names may only contain ASCII letters, " f"digits, and underscores."
        )


def validate_function_name(value: Optional[str]) -> None:
    """
    Validates that the function name is valid.

    Valid function names are non-empty and
    match the regex: [0-9A-Za-z_]*

    :param value: The function name to validate.

    :raises ValueError: If the function name is invalid.
    """
    if not value:
        raise ValueError("The function name cannot be `None` or empty")

    if not re_match(FUNCTION_NAME_REGEX, value):
        raise ValueError(
            f"Invalid function name: {value}. Function "
            f"names may only contain ASCII letters, "
            f"digits, and underscores."
        )


def validate_function_param_name(value: Optional[str]) -> None:
    """
    Validates that the function parameter name is valid.

    Valid function parameter names are non-empty and
    match the regex: [0-9A-Za-z_]*

    :param value: The function parameter name to validate.

    :raises ValueError: If the function parameter name is invalid.
    """
    if not value:
        raise ValueError("The function parameter name cannot be `None` or empty")

    if not re_match(FUNCTION_PARAM_NAME_REGEX, value):
        raise ValueError(
            f"Invalid function parameter name: {value}. Function parameter "
            f"names may only contain ASCII letters, digits, and underscores."
        )

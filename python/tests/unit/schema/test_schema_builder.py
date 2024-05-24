# Copyright (c) Microsoft. All rights reserved.

import pytest
import json
from unittest.mock import Mock
from typing import Any, List, Dict, Set, Tuple, Union, Optional

from semantic_kernel.kernel_pydantic import KernelBaseModel
from semantic_kernel.schema.kernel_json_schema_builder import KernelJsonSchemaBuilder


class ExampleModel(KernelBaseModel):
    name: str
    age: int


class AnotherModel:
    title: str
    score: float


def test_build_with_kernel_base_model():
    expected_schema = {"type": "object", "properties": {"name": {"type": "string"}, "age": {"type": "integer"}}}
    result = KernelJsonSchemaBuilder.build(ExampleModel)
    assert result == expected_schema


def test_build_with_model_with_annotations():
    expected_schema = {"type": "object", "properties": {"title": {"type": "string"}, "score": {"type": "number"}}}
    result = KernelJsonSchemaBuilder.build(AnotherModel)
    assert result == expected_schema


def test_build_with_primitive_type():
    expected_schema = {"type": "string"}
    result = KernelJsonSchemaBuilder.build(str)
    assert result == expected_schema
    result = KernelJsonSchemaBuilder.build("str")
    assert result == expected_schema

    expected_schema = {"type": "integer"}
    result = KernelJsonSchemaBuilder.build(int)
    assert result == expected_schema
    result = KernelJsonSchemaBuilder.build("int")
    assert result == expected_schema


def test_build_with_primitive_type_and_description():
    expected_schema = {"type": "string", "description": "A simple string"}
    result = KernelJsonSchemaBuilder.build(str, description="A simple string")
    assert result == expected_schema


def test_build_model_schema():
    expected_schema = {
        "type": "object",
        "properties": {"name": {"type": "string"}, "age": {"type": "integer"}},
        "description": "A model",
    }
    result = KernelJsonSchemaBuilder.build_model_schema(ExampleModel, description="A model")
    assert result == expected_schema


def test_build_from_type_name():
    expected_schema = {"type": "string", "description": "A simple string"}
    result = KernelJsonSchemaBuilder.build_from_type_name("str", description="A simple string")
    assert result == expected_schema


def test_get_json_schema():
    expected_schema = {"type": "string"}
    result = KernelJsonSchemaBuilder.get_json_schema(str)
    assert result == expected_schema

    expected_schema = {"type": "integer"}
    result = KernelJsonSchemaBuilder.get_json_schema(int)
    assert result == expected_schema


class MockModel:
    __annotations__ = {
        "id": int,
        "name": str,
        "is_active": bool,
        "scores": List[int],
        "metadata": Dict[str, Any],
        "tags": Set[str],
        "coordinates": Tuple[int, int],
        "status": Union[int, str],
        "optional_field": Optional[str],
    }
    __fields__ = {
        "id": Mock(description="The ID of the model"),
        "name": Mock(description="The name of the model"),
        "is_active": Mock(description="Whether the model is active"),
        "tags": Mock(description="Tags associated with the model"),
        "status": Mock(description="The status of the model, either as an integer or a string"),
        "scores": Mock(description="The scores associated with the model"),
        "optional_field": Mock(description="An optional field that can be null"),
        "metadata": Mock(description="The optional metadata description"),
    }


def test_build_primitive_types():
    assert KernelJsonSchemaBuilder.build(int) == {"type": "integer"}
    assert KernelJsonSchemaBuilder.build(str) == {"type": "string"}
    assert KernelJsonSchemaBuilder.build(bool) == {"type": "boolean"}
    assert KernelJsonSchemaBuilder.build(float) == {"type": "number"}


def test_build_list():
    schema = KernelJsonSchemaBuilder.build(list[str])
    assert schema == {"type": "array", "items": {"type": "string"}, "description": None}


def test_build_dict():
    schema = KernelJsonSchemaBuilder.build(dict[str, int])
    assert schema == {"type": "object", "additionalProperties": {"type": "integer"}, "description": None}


def test_build_set():
    schema = KernelJsonSchemaBuilder.build(set[int])
    assert schema == {"type": "array", "items": {"type": "integer"}, "description": None}


def test_build_tuple():
    schema = KernelJsonSchemaBuilder.build(Tuple[int, str])
    assert schema == {"type": "array", "items": [{"type": "integer"}, {"type": "string"}], "description": None}


def test_build_union():
    schema = KernelJsonSchemaBuilder.build(Union[int, str])
    assert schema == {"anyOf": [{"type": "integer"}, {"type": "string"}], "description": None}


def test_build_optional():
    schema = KernelJsonSchemaBuilder.build(Optional[int])
    assert schema == {"type": "integer", "nullable": True}


def test_build_model_schema_for_many_types():
    schema = KernelJsonSchemaBuilder.build(MockModel)
    expected = """
{
    "type": "object",
    "properties": {
        "id": {
            "type": "integer",
            "description": "The ID of the model"
        },
        "name": {
            "type": "string",
            "description": "The name of the model"
        },
        "is_active": {
            "type": "boolean",
            "description": "Whether the model is active"
        },
        "scores": {
            "type": "array",
            "items": {"type": "integer"},
            "description": "The scores associated with the model"
        },
        "metadata": {
            "type": "object",
            "additionalProperties": {
                "type": "object",
                "properties": {}
            },
            "description": "The optional metadata description"
        },
        "tags": {
            "type": "array",
            "items": {"type": "string"},
            "description": "Tags associated with the model"
        },
        "coordinates": {
            "type": "array",
            "items": {
                "type": "integer",
                "type": "integer"
            },
            "description": None
        },
        "status": {
            "anyOf": [
                {"type": "integer"},
                {"type": "string"}
            ],
            "description": "The status of the model, either as an integer or a string"
        },
        "optional_field": {
            "type": "string",
            "nullable": true,
            "description": "An optional field that can be null"
        }
    }
}
"""
    expected_schema = json.loads(expected)
    assert schema == expected_schema


def test_build_from_many_type_names():
    assert KernelJsonSchemaBuilder.build_from_type_name("int") == {"type": "integer"}
    assert KernelJsonSchemaBuilder.build_from_type_name("str") == {"type": "string"}
    assert KernelJsonSchemaBuilder.build_from_type_name("bool") == {"type": "boolean"}
    assert KernelJsonSchemaBuilder.build_from_type_name("float") == {"type": "number"}
    assert KernelJsonSchemaBuilder.build_from_type_name("list") == {"type": "array"}
    assert KernelJsonSchemaBuilder.build_from_type_name("dict") == {"type": "object"}
    assert KernelJsonSchemaBuilder.build_from_type_name("object") == {"type": "object"}
    assert KernelJsonSchemaBuilder.build_from_type_name("array") == {"type": "array"}


def test_get_json_schema_multiple():
    assert KernelJsonSchemaBuilder.get_json_schema(int) == {"type": "integer"}
    assert KernelJsonSchemaBuilder.get_json_schema(str) == {"type": "string"}
    assert KernelJsonSchemaBuilder.get_json_schema(bool) == {"type": "boolean"}
    assert KernelJsonSchemaBuilder.get_json_schema(float) == {"type": "number"}

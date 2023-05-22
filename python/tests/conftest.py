# Copyright (c) Microsoft. All rights reserved.

import os

import pytest

import semantic_kernel as sk

def pytest_collection_modifyitems(config, items):
    if platform.system() != "Linux":
        for item in items:
            if (
                item.parent
                and item.parent.name
                == "integration/connectors/memory/test_weaviate_memory_store.py"
            ):
                item.add_marker(
                    pytest.mark.skip(
                        reason="test_weaviate_memory_store uses embedded weaviate which only runs on Linux at the moment"
                    )
                )

@pytest.fixture(scope="session")
def create_kernel():
    kernel = sk.Kernel()
    return kernel


@pytest.fixture(scope="session")
def get_aoai_config():
    if "Python_Integration_Tests" in os.environ:
        deployment_name = os.environ["AzureOpenAIEmbeddings__DeploymentName"]
        api_key = os.environ["AzureOpenAI__ApiKey"]
        endpoint = os.environ["AzureOpenAI__Endpoint"]
    else:
        # Load credentials from .env file
        deployment_name, api_key, endpoint = sk.azure_openai_settings_from_dot_env()
        deployment_name = "text-embedding-ada-002"

    return deployment_name, api_key, endpoint


@pytest.fixture(scope="session")
def get_oai_config():
    if "Python_Integration_Tests" in os.environ:
        api_key = os.environ["OpenAI__ApiKey"]
        org_id = None
    else:
        # Load credentials from .env file
        api_key, org_id = sk.openai_settings_from_dot_env()

    return api_key, org_id

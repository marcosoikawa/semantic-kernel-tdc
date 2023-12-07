import asyncio

from openai import AsyncOpenAI

import semantic_kernel as sk
import semantic_kernel.connectors.ai as sk_ai
import semantic_kernel.connectors.ai.open_ai as sk_oai


async def create_assistant(client, api_key) -> sk_oai.OpenAIChatCompletion:
    assistant = sk_oai.OpenAIChatCompletion(
        ai_model_id="gpt-3.5-turbo-1106",
        api_key=api_key,
        is_assistant=True,
        async_client=client,
    )

    settings = sk_ai.OpenAIAssistantSettings(
        name="Parrot",
        description="A fun chat bot.",
        instructions="Repeat the user message in the voice of a pirate and then end with a parrot sound.",
    )
    await assistant.create_assistant_async(settings)
    return assistant


async def chat(kernel, chat_function) -> bool:
    context_vars = sk.ContextVariables()

    answer = await kernel.run_async(chat_function, input_vars=context_vars)
    print(f"Assistant:> {answer}")
    return True


async def main() -> None:
    api_key, _ = sk.openai_settings_from_dot_env()
    client = AsyncOpenAI(api_key=api_key)

    assistant = await create_assistant(client, api_key)

    kernel = sk.Kernel()
    kernel.add_chat_service("oai_assistant", assistant)

    prompt_config = sk.PromptTemplateConfig.from_completion_parameters(
        max_tokens=2000, temperature=0.7, top_p=0.8
    )

    messages = [
        "Fortune favors the bold.",
        "I came, I saw, I conquered.",
        "Practice makes perfect.",
    ]

    for message in messages:
        prompt_template = sk.ChatPromptTemplate(
            "{{$user_input}}", kernel.prompt_template_engine, prompt_config
        )
        prompt_template.add_user_message(message)
        function_config = sk.SemanticFunctionConfig(prompt_config, prompt_template)
        chat_function = kernel.register_semantic_function(
            "ChatBot", "Chat", function_config
        )
        print(f"User:> {message}")
        _ = await chat(kernel, chat_function)


if __name__ == "__main__":
    asyncio.run(main())

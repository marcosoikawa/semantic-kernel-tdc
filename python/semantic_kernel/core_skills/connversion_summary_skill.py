from semantic_kernel.orchestration.sk_context import SKContext
from semantic_kernel.skill_definition import sk_function
from semantic_kernel.kernel import Kernel
from semantic_kernel.text import text_chunker

from python.semantic_kernel.text.function_extension import aggregate_chunked_results_async


class ConversationSummarySkill:
    """
    Semantic skill that enables conversations summarization.
    """

    # The max tokens to process in a single semantic function call.
    MaxTokens = 1024

    SummarizeConversationPromptTemplate = (
        "BEGIN CONTENT TO SUMMARIZE:\n"
        "{{" + "$INPUT" + "}}\n"
        "END CONTENT TO SUMMARIZE.\n"
        "Summarize the conversation in 'CONTENT TO SUMMARIZE', identifying main points of discussion and any conclusions that were reached.\n"
        "Do not incorporate other general knowledge.\n"
        "Summary is in plain text, in complete sentences, with no markup or tags.\n"
        "\nBEGIN SUMMARY:\n"
    )

    def __init__(self,
                 kernel: Kernel):

        self._summarizeConversationFunction = kernel.create_semantic_function(
            ConversationSummarySkill.SummarizeConversationPromptTemplate,
            skillName=ConversationSummarySkill.__name__,
            description="Given a section of a conversation transcript, summarize the part of the conversation.",
            maxTokens=ConversationSummarySkill.MaxTokens,
            temperature=0.1,
            topP=0.5)

    @sk_function(
        description="Given a long conversation transcript, summarize the conversation.",
        name="SummarizeConversation",
        input_description="A long conversation transcript."
    )
    def SummarizeConversationAsync(self, input: str, context: SKContext) -> SKContext:
        """
        Given a long conversation transcript, summarize the conversation.

        :param input: A long conversation transcript.
        :param context: The SKContext for function execution.
        :return: SKContext with the summarized conversation result.
        """
        lines = text_chunker._split_text_lines(
            input, ConversationSummarySkill.MaxTokens)
        paragraphs = text_chunker._split_text_paragraph(
            lines, ConversationSummarySkill.MaxTokens)

        return aggregate_chunked_results_async(self._summarizeConversationFunction, paragraphs, context)

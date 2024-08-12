# Copyright (c) Microsoft. All rights reserved.

import asyncio
from enum import Enum
from typing import TYPE_CHECKING

from pydantic import Field

from semantic_kernel.agents.termination_strategy import TerminationStrategy
from semantic_kernel.contents.chat_message_content import ChatMessageContent
from semantic_kernel.kernel_pydantic import KernelBaseModel

if TYPE_CHECKING:
    from semantic_kernel.agents.agent import Agent


class AggregateTerminationCondition(str, Enum):
    """The condition for terminating the aggregation process."""

    ALL = "All"
    ANY = "Any"


class AggregatorTerminationStrategy(KernelBaseModel):
    """A strategy that aggregates multiple termination strategies."""

    strategies: list[TerminationStrategy]
    condition: AggregateTerminationCondition = Field(default=AggregateTerminationCondition.ALL)

    async def should_terminate_async(
        self,
        agent: "Agent",
        history: list[ChatMessageContent],
    ) -> bool:
        """Check if the agent should terminate."""
        strategy_execution = [strategy.should_terminate(agent, history) for strategy in self.strategies]
        results = await asyncio.gather(*strategy_execution)

        if self.condition == AggregateTerminationCondition.ALL:
            return all(results)
        return any(results)

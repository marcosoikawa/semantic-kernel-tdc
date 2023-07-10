from typing import TYPE_CHECKING

from semantic_kernel.kernel import Kernel
from semantic_kernel.planning.plan import Plan
from semantic_kernel.planning.planning_exception import PlanningException
from semantic_kernel.planning.sequential_planner.config import SequentialPlannerConfig
from semantic_kernel.planning.sequential_planner.extensions import (
    SequentialPlannerSKContextExtension as SKContextExtension,
)
from semantic_kernel.planning.sequential_planner.parser import SequentialPlanParser
from semantic_kernel.semantic_functions.prompt_template import PromptTemplate
from semantic_kernel.semantic_functions.prompt_template_config import (
    PromptTemplateConfig,
)
from semantic_kernel.semantic_functions.semantic_function_config import (
    SemanticFunctionConfig,
)

if TYPE_CHECKING:
    from semantic_kernel.orchestration.sk_context import SKContext
    from semantic_kernel.orchestration.sk_function_base import SKFunctionBase

SEQUENTIAL_PLANNER_DEFAULT_DESCRIPTION = (
    "Given a request or command or goal generate a step by step plan to "
    + "fulfill the request using functions. This ability is also known as decision making and function flow"
)


def read_prompt_file(prompt_path: str) -> str:
    with open(prompt_path, "r") as prompt_file:
        return prompt_file.read()


class SequentialPlanner:
    RESTRICTED_SKILL_NAME = "SequentialPlanner_Excluded"

    config: SequentialPlannerConfig
    _context: "SKContext"
    _function_flow_function: "SKFunctionBase"

    def __init__(
        self, kernel: Kernel, config: SequentialPlannerConfig = None, prompt: str = None
    ):
        assert isinstance(kernel, Kernel)
        self.config = config or SequentialPlannerConfig()

        self.config.excluded_skills.add(self.RESTRICTED_SKILL_NAME)

        self._function_flow_function = self._init_flow_function(prompt, kernel)

        self._context = kernel.create_new_context()

    def _init_flow_function(self, prompt: str, kernel: Kernel):
        prompt_config = PromptTemplateConfig.from_json(
            "Skills.SequentialPlanning.config.json"
        )
        prompt_template = prompt or read_prompt_file(
            "Skills.SequentialPlanning.skprompt.txt"
        )
        prompt_config.completion.max_tokens = self.config.max_tokens

        prompt_template = PromptTemplate(
            prompt_template, prompt_config, kernel.prompt_template_engine
        )
        function_config = SemanticFunctionConfig(prompt_config, prompt_template)

        return kernel.register_semantic_function(
            function_name=self.RESTRICTED_SKILL_NAME, function_config=function_config
        )

    async def create_plan_async(self, goal: str) -> Plan:
        if len(goal) == 0:
            raise PlanningException(
                PlanningException.ErrorCodes.InvalidGoal, "The goal specified is empty"
            )

        relevant_function_manual = await SKContextExtension.get_functions_manual_async(
            self._context, goal, self.config
        )
        self._context.variables.set("available_functions", relevant_function_manual)

        self._context.variables.update("goal")

        plan_result = await self._function_flow_function.invoke_async(self._context)

        if plan_result.error_occurred:
            raise PlanningException(
                PlanningException.ErrorCodes.CreatePlanError,
                f"Error creating plan for goal: {plan_result.last_error_description}",
                plan_result.last_exception,
            )

        plan_result_string = plan_result.result.strip()

        try:
            get_skill_function = (
                self.config.get_skill_function
                or SequentialPlanParser.get_skill_function(self._context)
            )
            plan = SequentialPlanParser.to_plan_from_xml(
                plan_result_string,
                goal,
                get_skill_function,
                self.config.allow_missing_functions,
            )

            if len(plan._steps) == 0:
                raise PlanningException(
                    PlanningException.ErrorCodes.CreatePlanError,
                    (
                        "Not possible to create plan for goal with available functions.\n",
                        f"Goal:{goal}\nFunctions:\n{relevant_function_manual}",
                    ),
                )

            return plan

        except PlanningException as e:
            if e.error_code == PlanningException.ErrorCodes.CreatePlanError:
                raise e
            elif e.error_code in [
                PlanningException.ErrorCodes.InvalidPlan,
                PlanningException.ErrorCodes.InvalidGoal,
            ]:
                raise PlanningException(
                    PlanningException.ErrorCodes.CreatePlanError,
                    "Unable to create plan",
                    e,
                )
            else:
                raise PlanningException(
                    PlanningException.ErrorCodes.CreatePlanError,
                    "Unable to create plan",
                    e,
                )

        except Exception as e:
            raise PlanningException(
                PlanningException.ErrorCodes.UnknownError,
                "Unknown error creating plan",
                e,
            )

# Copyright (c) Microsoft. All rights reserved.

import json
import logging
import os
import sys
from textwrap import dedent
from typing import Optional

if sys.version_info >= (3, 9):
    from typing import Annotated
else:
    from typing_extensions import Annotated

import regex

from semantic_kernel import Kernel
from semantic_kernel.connectors.ai.prompt_execution_settings import PromptExecutionSettings
from semantic_kernel.exceptions import (
    PlannerCreatePlanError,
    PlannerInvalidConfigurationError,
    PlannerInvalidGoalError,
    PlannerInvalidPlanError,
)
from semantic_kernel.functions.kernel_arguments import KernelArguments
from semantic_kernel.functions.kernel_function import KernelFunction
from semantic_kernel.functions.kernel_function_decorator import kernel_function
from semantic_kernel.functions.kernel_function_metadata import KernelFunctionMetadata
from semantic_kernel.functions.kernel_parameter_metadata import KernelParameterMetadata
from semantic_kernel.planners.action_planner.action_planner_config import ActionPlannerConfig
from semantic_kernel.planners.plan import Plan

logger: logging.Logger = logging.getLogger(__name__)


class ActionPlanner:
    """
    Action Planner allows to select one function out of many, to achieve a given goal.
    The planner implements the Intent Detection pattern, uses the functions registered
    in the kernel to see if there's a relevant one, providing instructions to call the
    function and the rationale used to select it. The planner can also return
    "no function" if nothing relevant is available.
    """

    RESTRICTED_PLUGIN_NAME = "ActionPlanner_Excluded"
    config: ActionPlannerConfig
    _stop_sequence: str = "#END-OF-PLAN"

    _planner_function: KernelFunction

    _kernel: Kernel
    _prompt_template: str

    def __init__(
        self,
        kernel: Kernel,
        service_id: str,
        config: Optional[ActionPlannerConfig] = None,
        prompt: Optional[str] = None,
        **kwargs,
    ) -> None:
        if kernel is None:
            raise PlannerInvalidConfigurationError("Kernel cannot be `None`.")

        self.config = config or ActionPlannerConfig()

        __cur_dir = os.path.dirname(os.path.abspath(__file__))
        __prompt_file = os.path.join(__cur_dir, "skprompt.txt")

        self._prompt_template = prompt if prompt else open(__prompt_file, "r").read()

        execute_settings = PromptExecutionSettings(
            service_id=service_id,
            extension_data={"max_tokens": self.config.max_tokens, "stop_sequences": self._stop_sequence},
        )

        self._planner_function = kernel.create_function_from_prompt(
            function_name="ActionPlanner",
            plugin_name=self.RESTRICTED_PLUGIN_NAME,
            prompt=self._prompt_template,
            prompt_execution_settings=execute_settings,
        )
        kernel.import_plugin_from_object(self, self.RESTRICTED_PLUGIN_NAME)

        self._kernel = kernel
        self._arguments = KernelArguments()

    async def create_plan(self, goal: str) -> Plan:
        """
        :param goal: The input to the planner based on which the plan is made
        :return: a Plan object
        """

        if not goal:
            raise PlannerInvalidGoalError("Goal cannot be `None`.")

        logger.info(f"Finding the best function for achieving the goal: {goal}")

        self._arguments["goal"] = goal

        generated_plan_raw = await self._planner_function.invoke(self._kernel, self._arguments)
        generated_plan_raw_str = str(generated_plan_raw)

        if not generated_plan_raw or not generated_plan_raw_str:
            raise PlannerCreatePlanError("No plan has been generated.")

        logger.info(f"Plan generated by ActionPlanner:\n{generated_plan_raw_str}")

        # Ignore additional text around JSON recursively
        json_regex = r"\{(?:[^{}]|(?R))*\}"
        generated_plan_str = regex.search(json_regex, generated_plan_raw_str)

        if not generated_plan_str:
            raise PlannerInvalidPlanError(f"No valid plan has been generated. Plan is:  {generated_plan_raw_str}")

        generated_plan_str = generated_plan_str.group()
        generated_plan_str = generated_plan_str.replace('""', '"')

        try:
            generated_plan = json.loads(generated_plan_str)
        except json.decoder.JSONDecodeError as e:
            raise PlannerInvalidPlanError("Encountered an error while parsing Plan JSON.") from e

        logger.info(f"Python dictionary of plan generated by ActionPlanner:\n{generated_plan}")

        if not generated_plan["plan"]:
            raise PlannerCreatePlanError("Suitable plan not generated by ActionPlanner.")

        if not generated_plan["plan"]["function"]:
            # no suitable function identified, returning plan with no steps
            logger.warn("No suitable function has been identified by ActionPlanner.")
            plan = Plan(description=goal)
        elif "." in generated_plan["plan"]["function"]:
            plugin, fun = generated_plan["plan"]["function"].split(".")
            function_ref = self._kernel.plugins[plugin][fun]
            logger.info(
                f"ActionPlanner has picked {plugin}.{fun}. Reference to this function"
                f" found in context: {function_ref}"
            )
            plan = Plan(description=goal, function=function_ref)
        else:
            plugin, func = generated_plan["plan"]["function"]
            function_ref = self._kernel.plugins[plugin][func]
            logger.info(
                f"ActionPlanner has picked {generated_plan['plan']['function']}.       "
                "              Reference to this function found in context:"
                f" {function_ref}"
            )
            plan = Plan(description=goal, function=function_ref)


        if "parameters" in generated_plan['plan']:
            for key, val in generated_plan["plan"]["parameters"].items():
                logger.info(f"Parameter {key}: {val}")
                if val:
                    plan.parameters[key] = str(val)
                    plan.state[key] = str(val)

        return plan

    @kernel_function(description="List a few good examples of plans to generate", name="GoodExamples")
    def good_examples(self, goal: Annotated[str, "The current goal processed by the planner"]) -> str:
        return dedent(
            """
            [EXAMPLE]
            - List of functions:
            // Get the current time.
            TimePlugin.Time
            No parameters.
            // Makes a POST request to a uri.
            HttpPlugin.PostAsync
            Parameter ""body"": The body of the request.
            - End list of functions.
            Goal: get the current time.
            {""plan"":{
            ""rationale"": ""the list contains a function that gets the current time (now)"",
            ""function"": ""TimePlugin.Time""
            }}
            #END-OF-PLAN
            """
        )

    @kernel_function(
        description="List a few edge case examples of plans to handle",
        name="EdgeCaseExamples",
    )
    def edge_case_examples(self, goal: Annotated[str, "The current goal processed by the planner"]) -> str:
        return dedent(
            '''
            [EXAMPLE]
            - List of functions:
            // Get the current time.
            TimePlugin.Time
            No parameters.
            // Write a file.
            FileIOPlugin.WriteAsync
            Parameter ""path"": Destination file. (default value: sample.txt)
            Parameter ""content"": File content.
            // Makes a POST request to a uri.
            HttpPlugin.PostAsync
            Parameter ""body"": The body of the request.
            - End list of functions.
            Goal: tell me a joke.
            {""plan"":{
            ""rationale"": ""the list does not contain functions to tell jokes or something funny"",
            ""function"": """",
            ""parameters"": {
            }}}
            #END-OF-PLAN
            '''
        )

    @kernel_function(description="List all functions available in the kernel", name="ListOfFunctions")
    def list_of_functions(self, goal: Annotated[str, "The current goal processed by the planner"]) -> str:
        available_functions = [
            self._create_function_string(func)
            for func in self._kernel.plugins.get_list_of_function_metadata()
            if (
                func.plugin_name != self.RESTRICTED_PLUGIN_NAME
                and func.plugin_name not in self.config.excluded_plugins
                and func.name not in self.config.excluded_functions
            )
        ]

        available_functions_str = "\n".join(available_functions)

        logger.info(f"List of available functions:\n{available_functions_str}")

        return available_functions_str

    def _create_function_string(self, function: KernelFunctionMetadata) -> str:
        """
        Takes an instance of KernelFunctionMetadata and returns a string that consists of
        function name, function description and parameters in the following format
        // <function-description>
        <plugin-name>.<function-name>
        Parameter ""<parameter-name>"": <parameter-description> (default value: `default_value`)
        ...

        :param function: An instance of KernelFunctionMetadata for which the string representation
            needs to be generated
        :return: string representation of function
        """

        if not function.description:
            logger.warn(f"{function.plugin_name}.{function.name} is missing a description")
            description = f"// Function {function.plugin_name}.{function.name}."
        else:
            description = f"// {function.description}"

        # add trailing period for description if not present
        if description[-1] != ".":
            description = f"{description}."

        name = f"{function.plugin_name}.{function.name}"

        parameters_list = [
            result for x in function.parameters if (result := self._create_parameter_string(x)) is not None
        ]

        if len(parameters_list) == 0:
            parameters = "No parameters."
        else:
            parameters = "\n".join(parameters_list)

        func_str = f"{description}\n{name}\n{parameters}"

        return func_str

    def _create_parameter_string(self, parameter: KernelParameterMetadata) -> str:
        """
        Takes an instance of ParameterView and returns a string that consists of
        parameter name, parameter description and default value for the parameter
        in the following format
        Parameter ""<parameter-name>"": <parameter-description> (default value: <default-value>)

        :param parameter: An instance of ParameterView for which the string representation needs to be generated
        :return: string representation of parameter
        """

        name = parameter.name
        description = desc if (desc := parameter.description) else name

        # add trailing period for description if not present
        if description[-1] != ".":
            description = f"{description}."

        default_value = f"(default value: {val})" if (val := parameter.default_value) else ""

        param_str = f'Parameter ""{name}"": {description} {default_value}'

        return param_str.strip()

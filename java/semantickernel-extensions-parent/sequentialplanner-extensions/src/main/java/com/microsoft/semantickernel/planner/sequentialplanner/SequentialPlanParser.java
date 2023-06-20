// Copyright (c) Microsoft. All rights reserved.
package com.microsoft.semantickernel.planner.sequentialplanner;

import com.microsoft.semantickernel.builders.SKBuilders;
import com.microsoft.semantickernel.orchestration.SKContext;
import com.microsoft.semantickernel.orchestration.SKFunction;
import com.microsoft.semantickernel.orchestration.WritableContextVariables;
import com.microsoft.semantickernel.planner.PlanningException;
import com.microsoft.semantickernel.planner.actionplanner.Plan;
import com.microsoft.semantickernel.skilldefinition.ReadOnlySkillCollection;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.w3c.dom.Document;
import org.w3c.dom.Node;
import org.w3c.dom.NodeList;
import org.xml.sax.SAXException;

import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

import javax.xml.parsers.DocumentBuilder;
import javax.xml.parsers.DocumentBuilderFactory;
import javax.xml.parsers.ParserConfigurationException;

/** Parse sequential plan text into a plan. */
public class SequentialPlanParser {
    private static final Logger LOGGER = LoggerFactory.getLogger(SequentialPlanParser.class);

    // The tag name used in the plan xml for the user's goal/ask.
    private static final String GoalTag = "goal";

    // The tag name used in the plan xml for the solution.
    private static final String SolutionTag = "plan";

    // The tag name used in the plan xml for a step that calls a skill function.
    private static final String FunctionTag = "function.";

    // The attribute tag used in the plan xml for setting the context variable name to set the
    // output of a function to.
    private static final String SetContextVariableTag = "setContextVariable".toLowerCase();

    // The attribute tag used in the plan xml for appending the output of a function to the final
    // result for a plan.
    private static final String AppendToResultTag = "appendToResult".toLowerCase();

    /**
     * Convert a plan xml string to a plan
     *
     * @param xmlString The plan xml string
     * @param goal The goal for the plan
     * @param context The semantic kernel context
     * @return The plan
     * @throws PlanningException
     */
    public static Plan toPlanFromXml(String xmlString, String goal, SKContext context)
            throws PlanningException {

        try {
            DocumentBuilder db = DocumentBuilderFactory.newInstance().newDocumentBuilder();

            Document doc =
                    db.parse(
                            new ByteArrayInputStream(
                                    ("<xml>" + xmlString + "</xml>")
                                            .getBytes(StandardCharsets.UTF_8)));

            NodeList solution = doc.getElementsByTagName(SolutionTag);

            Plan plan = new Plan(goal, context::getSkills);

            for (int i = 0; i < solution.getLength(); i++) {
                Node solutionNode = solution.item(i);
                String parentNodeName = solutionNode.getNodeName();

                for (int j = 0; j < solutionNode.getChildNodes().getLength(); j++) {
                    Node childNode = solutionNode.getChildNodes().item(j);
                    if (childNode.getNodeName().equals("#text")) {
                        if (childNode.getNodeValue() != null
                                && !childNode.getNodeValue().trim().isEmpty()) {
                            plan.addSteps(
                                    new Plan(childNode.getNodeValue().trim(), context::getSkills));
                        }
                        continue;
                    }

                    if (childNode.getNodeName().toLowerCase().startsWith(FunctionTag)) {
                        String[] skillFunctionNameParts =
                                childNode.getNodeName().split(FunctionTag);
                        String skillFunctionName = "";

                        if (skillFunctionNameParts.length > 1) {
                            skillFunctionName = skillFunctionNameParts[1];
                        }

                        String skillName = getSkillName(skillFunctionName);
                        String functionName = getFunctionName(skillFunctionName);

                        ReadOnlySkillCollection skills = context.getSkills();
                        if (functionName != null
                                && !functionName.isEmpty()
                                && skills.hasFunction(skillName, functionName)) {
                            SKFunction skillFunction =
                                    context.getSkills()
                                            .getFunctions(skillName)
                                            .getFunction(functionName, SKFunction.class);

                            WritableContextVariables functionVariables =
                                    SKBuilders.variables().build().writableClone();

                            skillFunction
                                    .describe()
                                    .getParameters()
                                    .forEach(
                                            p -> {
                                                functionVariables.setVariable(
                                                        p.getName(), p.getDefaultValue());
                                            });

                            List<String> functionOutputs = new ArrayList<>();
                            List<String> functionResults = new ArrayList<>();

                            if (childNode.getAttributes() != null) {

                                for (int k = 0; k < childNode.getAttributes().getLength(); k++) {
                                    Node attr = childNode.getAttributes().item(k);

                                    LOGGER.trace(
                                            "{}: processing attribute {}",
                                            parentNodeName,
                                            attr.toString());

                                    if (attr.getNodeName()
                                            .toLowerCase()
                                            .equals(SetContextVariableTag)) {
                                        functionOutputs.add(attr.getTextContent());
                                    } else if (attr.getNodeName()
                                            .toLowerCase()
                                            .equals(AppendToResultTag)) {
                                        functionOutputs.add(attr.getTextContent());
                                        functionResults.add(attr.getTextContent());
                                    } else {
                                        functionVariables.setVariable(
                                                attr.getNodeName(), attr.getTextContent());
                                    }
                                }
                            }

                            Plan planStep =
                                    new Plan(
                                            skillFunction,
                                            functionVariables,
                                            SKBuilders.variables().build(),
                                            functionOutputs,
                                            context::getSkills);

                            plan.addOutputs(functionResults);
                            plan.addSteps(planStep);
                        } else {
                            LOGGER.trace(
                                    "{}: appending function node {}",
                                    parentNodeName,
                                    skillFunctionName);
                            plan.addSteps(new Plan(childNode.getTextContent(), context::getSkills));
                        }

                        continue;
                    }

                    plan.addSteps(new Plan(childNode.getTextContent(), context::getSkills));
                }
            }
            return plan;

        } catch (RuntimeException | ParserConfigurationException | IOException | SAXException e) {
            throw new PlanningException(
                    PlanningException.ErrorCodes.InvalidPlan, "Failed to parse plan xml.", e);
        }
    }

    private static String getSkillName(String skillFunctionName) {
        String[] skillFunctionNameParts = skillFunctionName.split("\\.");
        return skillFunctionNameParts.length > 0 ? skillFunctionNameParts[0] : "";
    }

    private static String getFunctionName(String skillFunctionName) {
        String[] skillFunctionNameParts = skillFunctionName.split("\\.");
        return skillFunctionNameParts.length > 1 ? skillFunctionNameParts[1] : skillFunctionName;
    }
}

// Copyright (c) Microsoft. All rights reserved.
package com.microsoft.semantickernel.orchestration;

import static com.microsoft.semantickernel.skilldefinition.annotations.SKFunctionParameters.NO_DEFAULT_VALUE;

import com.microsoft.semantickernel.Kernel;
import com.microsoft.semantickernel.ai.AIException;
import com.microsoft.semantickernel.memory.SemanticTextMemory;
import com.microsoft.semantickernel.skilldefinition.KernelSkillsSupplier;
import com.microsoft.semantickernel.skilldefinition.ParameterView;
import com.microsoft.semantickernel.skilldefinition.ReadOnlySkillCollection;
import com.microsoft.semantickernel.skilldefinition.annotations.DefineSKFunction;
import com.microsoft.semantickernel.skilldefinition.annotations.SKFunctionInputAttribute;
import com.microsoft.semantickernel.skilldefinition.annotations.SKFunctionParameters;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import reactor.core.publisher.Mono;

import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.lang.reflect.Parameter;
import java.util.*;
import java.util.stream.Collectors;

import javax.annotation.Nullable;

/// <summary>
/// Standard Semantic Kernel callable function.
/// SKFunction is used to extend one C# <see cref="Delegate"/>, <see cref="Func{T, TResult}"/>, <see
// cref="Action"/>,
/// with additional methods required by the kernel.
/// </summary>
public class NativeSKFunction extends AbstractSkFunction<Void, SemanticSKContext> {

    private static final Logger LOGGER = LoggerFactory.getLogger(NativeSKFunction.class);

    private final SKNativeTask<SemanticSKContext> function;

    public NativeSKFunction(
            AbstractSkFunction.DelegateTypes delegateType,
            SKNativeTask<SemanticSKContext> delegateFunction,
            List<ParameterView> parameters,
            String skillName,
            String functionName,
            String description,
            KernelSkillsSupplier skillCollection) {
        super(delegateType, parameters, skillName, functionName, description, skillCollection);
        // TODO
        // Verify.NotNull(delegateFunction, "The function delegate is empty");
        // Verify.ValidSkillName(skillName);
        // Verify.ValidFunctionName(functionName);
        // Verify.ParametersUniqueness(parameters);

        this.function = delegateFunction;
    }

    @Override
    public Class<Void> getType() {
        return Void.class;
    }

    @Override
    public void registerOnKernel(Kernel kernel) {
        // No actions needed
    }

    private static class MethodDetails {
        public final boolean hasSkFunctionAttribute;
        public final AbstractSkFunction.DelegateTypes type;
        public final SKNativeTask<SemanticSKContext> function;
        public final List<ParameterView> parameters;
        public final String name;
        public final String description;

        private MethodDetails(
                boolean hasSkFunctionAttribute,
                AbstractSkFunction.DelegateTypes type,
                SKNativeTask<SemanticSKContext> function,
                List<ParameterView> parameters,
                String name,
                String description) {
            this.hasSkFunctionAttribute = hasSkFunctionAttribute;
            this.type = type;
            this.function = function;
            this.parameters = parameters;
            this.name = name;
            this.description = description;
        }
    }

    public static NativeSKFunction fromNativeMethod(
            Method methodSignature,
            Object methodContainerInstance,
            String skillName,
            KernelSkillsSupplier kernelSkillsSupplier) {
        if (skillName == null || skillName.isEmpty()) {
            skillName = ReadOnlySkillCollection.GlobalSkill;
        }

        MethodDetails methodDetails = getMethodDetails(methodSignature, methodContainerInstance);

        // If the given method is not a valid SK function
        if (!methodSignature.isAnnotationPresent(DefineSKFunction.class)) {
            throw new RuntimeException("Not a valid function");
        }

        return new NativeSKFunction(
                methodDetails.type,
                methodDetails.function,
                methodDetails.parameters,
                skillName,
                methodDetails.name,
                methodDetails.description,
                kernelSkillsSupplier);
    }

    @Override
    public SemanticSKContext buildContext(
            ContextVariables variables,
            @Nullable SemanticTextMemory memory,
            @Nullable ReadOnlySkillCollection skills) {
        return new DefaultSemanticSKContext(variables, memory, skills);
    }

    @Override
    public SemanticSKContext buildContext(SKContext toClone) {
        return new DefaultSemanticSKContext(
                toClone.getVariables(), toClone.getSemanticMemory(), toClone.getSkills());
    }

    // Run the native function
    @Override
    protected Mono<SemanticSKContext> invokeAsyncInternal(
            SemanticSKContext context, @Nullable Void settings) {
        return this.function.run(context);
    }

    private static MethodDetails getMethodDetails(
            Method methodSignature, Object methodContainerInstance) {
        // Verify.NotNull(methodSignature, "Method is NULL");

        // String name = methodSignature.getName();
        ArrayList<ParameterView> parameters = new ArrayList<>();

        boolean hasSkFunctionAttribute =
                methodSignature.isAnnotationPresent(DefineSKFunction.class);

        if (!hasSkFunctionAttribute) {
            throw new RuntimeException("method is not annotated with DefineSKFunction");
        }
        DelegateTypes type = getDelegateType(methodSignature);
        SKNativeTask<SemanticSKContext> function =
                getFunction(methodSignature, methodContainerInstance);

        // boolean hasStringParam =
        //    Arrays.asList(methodSignature.getGenericParameterTypes()).contains(String.class);

        String name = methodSignature.getAnnotation(DefineSKFunction.class).name();

        if (name == null || name.isEmpty()) {
            name = methodSignature.getName();
        }

        String description = methodSignature.getAnnotation(DefineSKFunction.class).description();

        return new MethodDetails(
                hasSkFunctionAttribute, type, function, parameters, name, description);
    }

    private static SKNativeTask<SemanticSKContext> getFunction(Method method, Object instance) {
        return (contextInput) -> {
            SemanticSKContext context = contextInput.copy();

            Set<Parameter> inputArgs = determineInputArgs(method);

            try {

                List<Object> args =
                        Arrays.stream(method.getParameters())
                                .map(
                                        parameter -> {
                                            if (SKContext.class.isAssignableFrom(
                                                    parameter.getType())) {
                                                return context; // .copy();
                                            } else {
                                                String value =
                                                        getArgumentValue(
                                                                method, context, parameter,
                                                                inputArgs);
                                                if (value != null) {
                                                    return value;
                                                } else {
                                                    throw new AIException(
                                                            AIException.ErrorCodes
                                                                    .InvalidConfiguration,
                                                            "Unknown arg " + parameter.getName());
                                                }
                                            }
                                        })
                                .collect(Collectors.toList());

                Mono mono;
                if (method.getReturnType().isAssignableFrom(Mono.class)) {
                    try {
                        mono = (Mono) method.invoke(instance, args.toArray());
                    } catch (IllegalAccessException | InvocationTargetException e) {
                        return Mono.error(e);
                    }
                } else {
                    try {
                        mono = Mono.just(method.invoke(instance, args.toArray()));
                    } catch (IllegalAccessException | InvocationTargetException e) {
                        return Mono.error(e);
                    }
                }

                return mono.map(
                        it -> {
                            if (it instanceof SKContext) {
                                return it;
                            } else {
                                return context.update((String) it);
                            }
                        });
            } catch (Exception e) {
                return Mono.error(e);
            }
        };
    }

    private static String getArgumentValue(
            Method method,
            SemanticSKContext context,
            Parameter parameter,
            Set<Parameter> inputArgs) {
        String variableName = getGetVariableName(parameter);

        Optional<String> arg = context.getVariables().get(variableName);
        if (arg.isPresent() == false) {
            // If this is bound to input get the input value
            if (inputArgs.contains(parameter)) {
                Optional<String> input = context.getVariables().get(ContextVariables.MAIN_KEY);
                if (input.isPresent()) {
                    arg = input;
                }
            }

            if (arg.isPresent() == false) {
                SKFunctionParameters annotation =
                        parameter.getAnnotation(SKFunctionParameters.class);
                if (annotation != null) {
                    arg = Optional.of(annotation.defaultValue());
                }
            }
        }

        if (arg.isPresent() == false && variableName.matches("arg\\d")) {
            LOGGER.warn(
                    "For the function "
                            + method.getDeclaringClass().getName()
                            + "."
                            + method.getName()
                            + ", the parameter argument name was detected as \""
                            + variableName
                            + "\" this indicates that the argument name for this function was"
                            + " removed during compilation. To support this function its arguments"
                            + " must be annotated with @SKFunctionParameters with the name defined,"
                            + " or @SKFunctionInputAttribute.");
        }

        if (NO_DEFAULT_VALUE.equals(arg.get())) {
            return null;
        }
        return arg.get();
    }

    private static String getGetVariableName(Parameter parameter) {
        SKFunctionParameters annotation = parameter.getAnnotation(SKFunctionParameters.class);

        if (annotation == null || annotation.name() == null || annotation.name().isEmpty()) {
            return parameter.getName();
        }
        return annotation.name();
    }

    private static Set<Parameter> determineInputArgs(Method method) {
        // Something is bound to the input if either:
        // - it is annotated with @SKFunctionInputAttribute
        // - SKFunctionParameters annotation has a name of "input"
        // - the arg name is "input"
        // - there is only 1 string argument to the function

        // Get all parameters annotated with SKFunctionInputAttribute
        List<Parameter> annotated =
                Arrays.stream(method.getParameters())
                        .filter(it -> it.isAnnotationPresent(SKFunctionInputAttribute.class))
                        .collect(Collectors.toList());

        if (annotated.size() > 1) {
            LOGGER.warn(
                    "Multiple arguments of "
                            + method.getName()
                            + " have the @SKFunctionInputAttribute annotation. This is likely an"
                            + " error.");
        }

        // Get all parameters annotated with SKFunctionParameters with a name of "input"
        List<Parameter> annotatedWithName =
                Arrays.stream(method.getParameters())
                        .filter(it -> it.isAnnotationPresent(SKFunctionParameters.class))
                        .filter(it -> it.getName().equals("input"))
                        .collect(Collectors.toList());

        if (annotatedWithName.size() > 1) {
            LOGGER.warn(
                    "Multiple arguments of "
                            + method.getName()
                            + " have the name input. This is likely an error.");
        }

        // Get all parameters named "input", this will frequently fail as compilers strip out
        // argument names
        List<Parameter> calledInput =
                Arrays.stream(method.getParameters())
                        .filter(it -> getGetVariableName(it).equals("input"))
                        .collect(Collectors.toList());

        // Get parameter if there is only 1 string, and it has not been annotated with
        // SKFunctionParameters
        List<Parameter> soloString =
                Arrays.stream(method.getParameters())
                        .filter(it -> it.getType().equals(String.class))
                        .filter(
                                it ->
                                        !(it.isAnnotationPresent(SKFunctionParameters.class)
                                                && !it.getAnnotation(SKFunctionParameters.class)
                                                        .name()
                                                        .isEmpty()))
                        .collect(Collectors.toList());
        if (soloString.size() > 1) {
            soloString.clear();
        }

        Set<Parameter> params = new HashSet<>();
        params.addAll(annotated);
        params.addAll(annotatedWithName);
        params.addAll(calledInput);
        params.addAll(soloString);

        if (params.size() > 1) {
            LOGGER.warn(
                    "Multiple arguments of "
                            + method.getName()
                            + " are bound to the input variable. This is likely an error.");
        }

        return params;
    }

    // Inspect a method and returns the corresponding delegate and related info
    private static AbstractSkFunction.DelegateTypes getDelegateType(Method method) {
        // TODO ALL TYPES
        if (method.getReturnType().equals(String.class)) {
            return AbstractSkFunction.DelegateTypes.OutString;
        }

        if (method.getReturnType().equals(Mono.class)) {
            return AbstractSkFunction.DelegateTypes.InSKContextOutTaskString;
        }

        throw new RuntimeException("Unknown function type");
    }
}

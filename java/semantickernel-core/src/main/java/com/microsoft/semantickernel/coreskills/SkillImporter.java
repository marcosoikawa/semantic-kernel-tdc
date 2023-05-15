// Copyright (c) Microsoft. All rights reserved.
package com.microsoft.semantickernel.coreskills;

import com.microsoft.semantickernel.orchestration.NativeSKFunction;
import com.microsoft.semantickernel.skilldefinition.FunctionCollection;
import com.microsoft.semantickernel.skilldefinition.ReadOnlySkillCollection;
import com.microsoft.semantickernel.skilldefinition.annotations.DefineSKFunction;

import reactor.core.publisher.Mono;

import java.util.Arrays;
import java.util.List;
import java.util.function.Supplier;
import java.util.stream.Collectors;

public class SkillImporter {

    public static FunctionCollection importSkill(
            Object skillInstance,
            String skillName,
            Supplier<ReadOnlySkillCollection> skillCollectionSupplier) {
        List<NativeSKFunction> methods =
                Arrays.stream(skillInstance.getClass().getMethods())
                        .filter(method -> method.isAnnotationPresent(DefineSKFunction.class))
                        .map(
                                method -> {
                                    if (!method.getReturnType().isAssignableFrom(Mono.class)) {
                                        throw new RuntimeException("Skill must return a Mono");
                                    }
                                    return NativeSKFunction.fromNativeMethod(
                                            method,
                                            skillInstance,
                                            skillName,
                                            skillCollectionSupplier);
                                })
                        .collect(Collectors.toList());

        return new FunctionCollection(skillName, methods);
    }
}

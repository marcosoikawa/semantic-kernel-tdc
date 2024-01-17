package com.microsoft.semantickernel.semanticfunctions;

import com.microsoft.semantickernel.templateengine.handlebars.HandlebarsPromptTemplate;
import com.microsoft.semantickernel.templateengine.semantickernel.DefaultPromptTemplate;
import reactor.util.annotation.NonNull;

import java.util.Locale;

public class KernelPromptTemplateFactory implements PromptTemplateFactory {

    public PromptTemplate tryCreate(@NonNull PromptTemplateConfig templateConfig) {
        if (templateConfig.getTemplateFormat() != null &&
                "semantic-kernel".equals(templateConfig.getTemplateFormat().toLowerCase(Locale.ROOT))) {
            return new DefaultPromptTemplate(templateConfig);
        }

        throw new UnknownTemplateFormatException(templateConfig.getTemplateFormat());
    }
}

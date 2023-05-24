package com.microsoft.semantickernel;

import com.azure.core.credential.AzureKeyCredential;
import com.azure.core.credential.TokenCredential;
import com.microsoft.openai.AzureOpenAIClient;
import com.microsoft.openai.OpenAIAsyncClient;
import com.microsoft.openai.OpenAIClientBuilder;
import com.microsoft.semantickernel.util.Settings;

import java.io.IOException;

public class Config {

    public static final String CONF_PROPERTIES = "java/samples/conf.properties";

    public enum ClientType {
        OPEN_AI {
            @Override
            public OpenAIAsyncClient getClient() throws IOException {
                Settings.OpenAISettings settings = Settings.getOpenAISettingsFromFile(CONF_PROPERTIES);

                return new OpenAIClientBuilder()
                        .setApiKey(settings.getKey())
                        .build();
            }
        },
        AZURE_OPEN_AI {
            @Override
            public OpenAIAsyncClient getClient()
                 throws IOException {
                    Settings.AzureOpenAISettings settings = Settings.getAzureOpenAISettingsFromFile(CONF_PROPERTIES);

                    return new AzureOpenAIClient(
                            new com.azure.ai.openai.OpenAIClientBuilder()
                                    .endpoint(settings.getEndpoint())
                                    .credential(new AzureKeyCredential(settings.getKey()))
                                    .buildAsyncClient());
            }
        };

        /**
         * Returns the client that will handle AzureOpenAI or OpenAI requests.
         *
         * @return client to be used by the kernel.
         */
        public abstract OpenAIAsyncClient getClient() throws IOException;
    }
}

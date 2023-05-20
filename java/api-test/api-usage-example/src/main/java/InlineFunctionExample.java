// Copyright (c) Microsoft. All rights reserved.
import com.azure.ai.openai.OpenAIClientBuilder;
import com.azure.core.credential.AzureKeyCredential;
import com.microsoft.openai.AzureOpenAIClient;
import com.microsoft.openai.OpenAIAsyncClient;
import com.microsoft.semantickernel.Kernel;
import com.microsoft.semantickernel.KernelConfig;
import com.microsoft.semantickernel.builders.SKBuilders;
import com.microsoft.semantickernel.semanticfunctions.PromptTemplateConfig;
import com.microsoft.semantickernel.textcompletion.CompletionSKFunction;
import com.microsoft.semantickernel.textcompletion.TextCompletion;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.Properties;
import java.util.concurrent.TimeUnit;

public class InlineFunctionExample {
    public static final String AZURE_CONF_PROPERTIES = "conf.properties";
    private static final Logger LOGGER = LoggerFactory.getLogger(InlineFunctionExample.class);
    private static final String MODEL = "text-davinci-003";

    private static String API_KEY = "";
    private static String ENDPOINT = "";

    static{
        try {
            API_KEY = getToken(AZURE_CONF_PROPERTIES);
            ENDPOINT = getEndpoint(AZURE_CONF_PROPERTIES);
        } catch (IOException e) {
            LOGGER.error("Error reading config file or properties ", e);
        }
    }

    private static final String TEXT_TO_SUMMARIZE = """
            Demo (ancient Greek poet)
               From Wikipedia, the free encyclopedia
               Demo or Damo (Greek: Δεμώ, Δαμώ; fl. c. AD 200) was a Greek woman of the
                Roman period, known for a single epigram, engraved upon the Colossus of
                Memnon, which bears her name. She speaks of herself therein as a lyric
                poetess dedicated to the Muses, but nothing is known of her life.[1]
               Identity
               Demo was evidently Greek, as her name, a traditional epithet of Demeter,
                signifies. The name was relatively common in the Hellenistic world, in
                Egypt and elsewhere, and she cannot be further identified. The date of her
                visit to the Colossus of Memnon cannot be established with certainty, but
                internal evidence on the left leg suggests her poem was inscribed there at
                some point in or after AD 196.[2]
               Epigram
               There are a number of graffiti inscriptions on the Colossus of Memnon.
                Following three epigrams by Julia Balbilla, a fourth epigram, in elegiac
                couplets, entitled and presumably authored by "Demo" or "Damo" (the
                Greek inscription is difficult to read), is a dedication to the Muses.[2]
                The poem is traditionally published with the works of Balbilla, though the
                internal evidence suggests a different author.[1]
               In the poem, Demo explains that Memnon has shown her special respect. In
                return, Demo offers the gift for poetry, as a gift to the hero. At the end
                of this epigram, she addresses Memnon, highlighting his divine status by
                recalling his strength and holiness.[2]
               Demo, like Julia Balbilla, writes in the artificial and poetic Aeolic
                dialect. The language indicates she was knowledgeable in Homeric
                poetry—'bearing a pleasant gift', for example, alludes to the use of that
                phrase throughout the Iliad and Odyssey.[a][2];
                """;

    private static String getToken(String configName) throws IOException {
        return getConfigValue(configName, "token");
    }

    private static String getEndpoint(String configName) throws IOException {
        return getConfigValue(configName, "endpoint");
    }

    private static String getConfigValue(String configName, String propertyName)
            throws IOException {
        String propertyValue = "";
        Path configPath = Paths.get(System.getProperty("user.home"), ".oai", configName);
        Properties props = new Properties();
        try (var reader = Files.newBufferedReader(configPath)) {
            props.load(reader);
            propertyValue = props.getProperty(propertyName);
            if (propertyValue == null) {
                throw new IOException("No property for: " + propertyName);
            }
        } catch (IOException e) {
            throw new IOException("Please create a file at " + configPath, e);
        }
        return propertyValue;
    }

    public static void main(String[] args) {
        if(API_KEY.isEmpty() || ENDPOINT.isEmpty()){
            LOGGER.error("Please provide API_KEY and ENDPOINT");
            return;
        }

        OpenAIAsyncClient client = new AzureOpenAIClient(
                new OpenAIClientBuilder()
                        .endpoint(ENDPOINT)
                        .credential(new AzureKeyCredential(API_KEY))
                        .buildAsyncClient());

        TextCompletion textCompletion = SKBuilders.textCompletionService().build(client, MODEL);
        String prompt = "{{$input}}\n" + "Summarize the content above.";

        KernelConfig kernelConfig = new KernelConfig.Builder()
                .addTextCompletionService(MODEL, kernel -> textCompletion)
                .build();

        Kernel kernel = SKBuilders.kernel().setKernelConfig(kernelConfig).build();

        CompletionSKFunction summarize = kernel.getSemanticFunctionBuilder()
                .createFunction(
                        prompt,
                        "summarize",
                        null,
                        null,
                        new PromptTemplateConfig.CompletionConfig(
                                0.2, 0.5, 0, 0, 2000, new ArrayList<>()));

        if (summarize == null) {
            LOGGER.error("Null function");
            return;
        }

        summarize.invokeAsync(TEXT_TO_SUMMARIZE).subscribe(
                context -> {
                    LOGGER.info("Result: {} ", context.getResult());
                },
                error -> {
                    LOGGER.error("Error: {} ", error.getMessage());
                },
                () -> {
                    LOGGER.info("Completed");
                });

        try {
            TimeUnit.SECONDS.sleep(10);
        } catch (InterruptedException e) {
            LOGGER.warn("Interrupted : {}", e.getMessage());
            Thread.currentThread().interrupt();
        }
    }
}
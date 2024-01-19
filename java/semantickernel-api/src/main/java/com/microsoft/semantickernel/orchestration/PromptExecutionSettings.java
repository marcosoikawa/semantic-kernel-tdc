package com.microsoft.semantickernel.orchestration;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import com.fasterxml.jackson.annotation.JsonProperty;

public class PromptExecutionSettings {

    public static final int DEFAULT_MAX_TOKENS = 256;
    public static final double DEFAULT_TEMPERATURE = 1.0;
    public static final double DEFAULT_TOP_P = 1.0;
    public static final double DEFAULT_PRESENCE_PENALTY = 0.0;
    public static final double DEFAULT_FREQUENCY_PENALTY = 0.0;
    public static final int DEFAULT_BEST_OF = 1;    

    private static final String SERVICE_ID = "service_id";
    private static final String MODEL_ID = "model_id";
    private static final String TEMPERATURE = "temperature";
    private static final String TOP_P = "top_p";
    private static final String PRESENCE_PENALTY = "presence_penalty";
    private static final String FREQUENCY_PENALTY = "frequency_penalty";
    private static final String MAX_TOKENS = "max_tokens";
    private static final String BEST_OF = "best_of";
    private static final String USER = "user";
    private static final String STOP_SEQUENCES = "stop_sequences";

    /// <summary>
    /// Service identifier.
    /// This identifies a service and is set when the AI service is registered.
    /// </summary>
    private final String serviceId;

    /// <summary>
    /// Model identifier.
    /// This identifies the AI model these settings are configured for e.g., gpt-4, gpt-3.5-turbo
    /// </summary>
    private final String modelId;

    private final double temperature;

    private final double topP;
    private final double presencePenalty;
    private final double frequencyPenalty;
    private final int maxTokens;
    private final int bestOf;
    private final String user;
    private final List<String> stopSequences;

    public PromptExecutionSettings(
        @JsonProperty(SERVICE_ID) String serviceId,
        @JsonProperty(MODEL_ID) String modelId,
        @JsonProperty(TEMPERATURE) double temperature,
        @JsonProperty(TOP_P) double topP,
        @JsonProperty(PRESENCE_PENALTY) double presencePenalty,
        @JsonProperty(FREQUENCY_PENALTY) double frequencyPenalty,
        @JsonProperty(MAX_TOKENS) int maxTokens,
        @JsonProperty(MODEL_ID) int bestOf,
        @JsonProperty(USER) String user,
        @JsonProperty(STOP_SEQUENCES) List<String> stopSequences) {
        this.serviceId = serviceId;
        this.modelId = modelId;
        this.temperature = temperature;
        this.topP = topP;
        this.presencePenalty = presencePenalty;
        this.frequencyPenalty = frequencyPenalty;
        this.maxTokens = maxTokens;
        this.bestOf = bestOf;
        this.user = user;
        this.stopSequences = stopSequences;
    }

    public String getServiceId() {
        return serviceId;
    }

    public String getModelId() {
        return modelId;
    }

    public double getTemperature() {
        return Double.isNaN(temperature) ? DEFAULT_TEMPERATURE : temperature;
    }

    public double getTopP() {
        return topP;
    }

    public double getPresencePenalty() {
        return presencePenalty;
    }

    public double getFrequencyPenalty() {
        return frequencyPenalty;
    }

    public int getMaxTokens() {
        return maxTokens;
    }

    public int getBestOf() {
        // TODO: not present in com.azure:azure-ai-openai
        return bestOf;
    }

    public String getUser() {
        return user;
    }

    public List<String> getStopSequences() {
        return stopSequences;
    }


    public static Builder builder() {
        return new Builder();
    }

    public static class Builder {

        Map<String, Object> settings = new HashMap<>();

        public Builder withServiceId(String serviceId) {
            settings.put(SERVICE_ID, serviceId);
            return this;
        }

        public Builder withModelId(String modelId) {
            settings.put(MODEL_ID, modelId);
            return this;
        }

        public Builder withTemperature(double temperature) {
            if (!Double.isNaN(temperature)) {
                settings.put(TEMPERATURE, temperature);
            }
            return this;
        }

        public Builder withTopP(double topP) {
            if (!Double.isNaN(topP)) {
                settings.put(TOP_P, topP);
            }
            return this;
        }

        public Builder withPresencePenalty(double presencePenalty) {
            if (!Double.isNaN(presencePenalty)) {
                settings.put(PRESENCE_PENALTY, presencePenalty);
            }
            return this;
        }

        public Builder withFrequencyPenalty(double frequencyPenalty) {
            if (!Double.isNaN(frequencyPenalty)) {
                settings.put(FREQUENCY_PENALTY, frequencyPenalty);
            }
            return this;
        }

        public Builder withMaxTokens(int maxTokens) {
            settings.put(MAX_TOKENS, maxTokens);
            return this;
        }

        public Builder withBestOf(int bestOf) {
            settings.put(BEST_OF, bestOf);
            return this;
        }

        public Builder withUser(String user) {
            settings.put(USER, user);
            return this;
        }

        @SuppressWarnings("unchecked")
        public Builder withStopSequences(List<String> stopSequences) {
            if (stopSequences != null) {
                ((List<String>)settings.computeIfAbsent(STOP_SEQUENCES, k -> new ArrayList<String>())).addAll(stopSequences);
            }
            return this;
        }

        @SuppressWarnings("unchecked")
        public PromptExecutionSettings build() {
            return new PromptExecutionSettings(
                (String)settings.getOrDefault(SERVICE_ID, ""),
                (String)settings.getOrDefault(MODEL_ID, ""),
                (double)settings.getOrDefault(TEMPERATURE, DEFAULT_TEMPERATURE),
                (double)settings.getOrDefault(TOP_P, DEFAULT_TOP_P),
                (double)settings.getOrDefault(PRESENCE_PENALTY, DEFAULT_PRESENCE_PENALTY),
                (double)settings.getOrDefault(FREQUENCY_PENALTY, DEFAULT_FREQUENCY_PENALTY),
                (int)settings.getOrDefault(MAX_TOKENS, DEFAULT_MAX_TOKENS),
                (int)settings.getOrDefault(BEST_OF, DEFAULT_BEST_OF),
                (String)settings.getOrDefault(USER, ""),
                (List<String>)settings.getOrDefault(STOP_SEQUENCES, Collections.emptyList())
            );
        }
    }
}

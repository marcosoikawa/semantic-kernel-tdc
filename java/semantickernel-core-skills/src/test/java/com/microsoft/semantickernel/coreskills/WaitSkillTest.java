package com.microsoft.semantickernel.coreskills;

import java.time.Duration;
import org.junit.jupiter.api.Test;
import reactor.core.publisher.Mono;
import reactor.test.StepVerifier;

class WaitSkillTest {

    WaitSkill waitSkill = new WaitSkill();

    @Test
    void secondsAsync_givenPositiveSeconds_shouldDelay() {
        Duration expectedDelay = Duration.ofMillis(1500);

        String seconds = "1.5";
        Mono<Void> result = waitSkill.wait(seconds);
        StepVerifier.create(result)
                .expectSubscription()
                .expectNoEvent(expectedDelay)
                .expectComplete()
                .verify();
    }

    @Test
    void secondsAsync_givenZeroSeconds_shouldNotDelay() {
        String seconds = "0";
        Mono<Void> result = waitSkill.wait(seconds);
        StepVerifier.create(result)
                .expectSubscription()
                .expectComplete()
                .verify();
    }

    @Test
    void secondsAsync_givenNegativeSeconds_shouldNotDelay() {
        String seconds = "-1.5";
        Mono<Void> result = waitSkill.wait(seconds);
        StepVerifier.create(result)
                .expectSubscription()
                .expectComplete()
                .verify();
    }
}
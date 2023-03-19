// Copyright (c) Microsoft Corporation. All rights reserved.

import { ICompleteRequestSettings } from './completeRequestSettings';

/**
 * Interface for text completion clients.
 */
export interface ITextCompletionClient {
    /**
     * Creates a completion for the prompt and settings.
     * @param text The prompt to complete
     * @param requestSettings Request settings for the completion API
     * @returns Promise of text generated by the remote model
     */
    complete(text: string, requestSettings: ICompleteRequestSettings): Promise<string>;
}

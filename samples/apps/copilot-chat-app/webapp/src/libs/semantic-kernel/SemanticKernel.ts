// Copyright (c) Microsoft. All rights reserved.

import { AuthHeaderTags } from '../../redux/features/plugins/PluginsState';
import { IAsk } from './model/Ask';
import { IAskResult } from './model/AskResult';

interface ServiceRequest {
    commandPath: string;
    method?: string;
    body?: unknown;
}

export class SemanticKernel {
    // eslint-disable-next-line @typescript-eslint/space-before-function-paren
    constructor(private readonly serviceUrl: string) {}

    public invokeAsync = async (
        ask: IAsk,
        skillName: string,
        functionName: string,
        accessToken: string,
        pluginAuth?: {
            headerTag: AuthHeaderTags;
            authData: string;
        },
    ): Promise<IAskResult> => {
        const result = await this.getResponseAsync<IAskResult>(
            {
                commandPath: `skills/${skillName}/functions/${functionName}/invoke`,
                method: 'POST',
                body: ask,
            },
            accessToken,
            pluginAuth,
        );
        return result;
    };

    private readonly getResponseAsync = async <T>(
        request: ServiceRequest,
        accessToken: string,
        pluginAuth?: {
            headerTag: AuthHeaderTags;
            authData: string;
        },
    ): Promise<T> => {
        const { commandPath, method, body } = request;
        const headers = new Headers({
            Authorization: `Bearer ${accessToken}`,
            'Content-Type': 'application/json',
        });

        // TODO: add all plug in tokens / auth data?
        if (pluginAuth) headers.append(`x-sk-copilot-${pluginAuth.headerTag}-authorization`, pluginAuth.authData);

        try {
            const requestUrl = new URL(commandPath, this.serviceUrl);
            const response = await fetch(requestUrl, {
                method: method ?? 'GET',
                body: JSON.stringify(body),
                headers: headers,
            });

            if (!response.ok) {
                throw Object.assign(new Error(response.statusText + ' => ' + (await response.text())));
            }

            return (await response.json()) as T;
        } catch (e) {
            var additional_error_msg = '';
            if (e instanceof TypeError) {
                // fetch() will reject with a TypeError when a network error is encountered.
                additional_error_msg =
                    '\n\nPlease check that your backend is running and that it is accessible by the app';
            }
            throw Object.assign(new Error(e + additional_error_msg));
        }
    };
}

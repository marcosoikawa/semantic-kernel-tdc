// Copyright (c) Microsoft. All rights reserved.

import { AdditionalApiProperties, AuthHeaderTags } from '../../redux/features/plugins/PluginsState';
import { IChatMessage } from '../models/ChatMessage';
import { IChatParticipant } from '../models/ChatParticipant';
import { IChatSession } from '../models/ChatSession';
import { IChatUser } from '../models/ChatUser';
import { IAsk, IAskVariables } from '../semantic-kernel/model/Ask';
import { IAskResult } from '../semantic-kernel/model/AskResult';
import { BaseService } from './BaseService';

export class ChatService extends BaseService {
    public createChatAsync = async (
        userId: string,
        title: string,
        accessToken: string,
    ): Promise<IChatSession> => {
        const body = {
            title: title,
        };

        const result = await this.getResponseAsync<IChatSession>(
            {
                commandPath: `chatSession/create/${userId}`,
                method: 'POST',
                body: body,
            },
            accessToken,
        );

        return result;
    };

    public getChatAsync = async (chatId: string, accessToken: string): Promise<IChatSession> => {
        const result = await this.getResponseAsync<IChatSession>(
            {
                commandPath: `chatSession/getChat/${chatId}`,
                method: 'GET',
            },
            accessToken,
        );

        return result;
    };

    public getAllChatsAsync = async (userId: string, accessToken: string): Promise<IChatSession[]> => {
        const result = await this.getResponseAsync<IChatSession[]>(
            {
                commandPath: `chatSession/getAllChats/${userId}`,
                method: 'GET',
            },
            accessToken,
        );
        return result;
    };

    public getChatMessagesAsync = async (
        chatId: string,
        startIdx: number,
        count: number,
        accessToken: string,
    ): Promise<IChatMessage[]> => {
        const result = await this.getResponseAsync<IChatMessage[]>(
            {
                commandPath: `chatSession/getChatMessages/${chatId}?startIdx=${startIdx}&count=${count}`,
                method: 'GET',
            },
            accessToken,
        );

        return result;
    };

    public editChatAsync = async (chatId: string, title: string, accessToken: string): Promise<any> => {
        const body: IChatSession = {
            id: chatId,
            title: title,
        };

        const result = await this.getResponseAsync<any>(
            {
                commandPath: `chatSession/edit`,
                method: 'POST',
                body: body,
            },
            accessToken,
        );

        return result;
    };

    public getBotResponseAsync = async (
        ask: IAsk,
        accessToken: string,
        enabledPlugins?: {
            headerTag: AuthHeaderTags;
            authData: string;
            apiProperties?: AdditionalApiProperties;
        }[],
    ): Promise<IAskResult> => {
        // If skill requires any additional api properties, append to context
        if (enabledPlugins && enabledPlugins.length > 0) {
            const openApiSkillVariables: IAskVariables[] = [];

            for (var idx in enabledPlugins) {
                var plugin = enabledPlugins[idx];

                if (plugin.apiProperties) {
                    const apiProperties = plugin.apiProperties;

                    for (var property in apiProperties) {
                        const propertyDetails = apiProperties[property];

                        if (propertyDetails.required && !propertyDetails.value) {
                            throw new Error(`Missing required property ${property} for ${plugin} skill.`);
                        }

                        if (propertyDetails.value) {
                            openApiSkillVariables.push({
                                key: `${property}`,
                                value: apiProperties[property].value!,
                            });
                        }
                    }
                }
            }

            ask.variables = ask.variables ? ask.variables.concat(openApiSkillVariables) : openApiSkillVariables;
        }

        const result = await this.getResponseAsync<IAskResult>(
            {
                commandPath: `chat`,
                method: 'POST',
                body: ask,
            },
            accessToken,
            enabledPlugins,
        );

        return result;
    };

    public joinChatAsync = async (userId: string, chatId: string, accessToken: string): Promise<any> => {
        const body: IChatParticipant = {
            userId: userId,
            chatId: chatId,
        };

        const result = await this.getResponseAsync<any>(
            {
                commandPath: `chatParticipant/join`,
                method: 'POST',
                body: body,
            },
            accessToken,
        );

        return result;
    };

    public getAllChatParticipantsAsync = async (chatId: string, accessToken: string): Promise<IChatUser[]> => {
        const result = await this.getResponseAsync<any>(
            {
                commandPath: `chatParticipant/getAllParticipants/${chatId}`,
                method: 'GET',
            },
            accessToken,
        );

        const chatUsers: IChatUser[] = result.map((participant: any) => {
            return {
                id: participant.userId,
                online: false,
                fullName: '',
                emailAddress: '',
                lastTypingTimestamp: 0,
            } as IChatUser;
        });

        return chatUsers;
    };
}

// Copyright (c) Microsoft. All rights reserved.

import { IChatMessage } from '../../../libs/models/ChatMessage';
import { IChatUser } from '../../../libs/models/ChatUser';

export interface ChatState {
    id: string;
    title: string;
    users: IChatUser[];
    messages: IChatMessage[];
    botProfilePicture: string;
    isTyping?: boolean;
}

export interface ConversationTypingState {
    id: string;
    isTyping: boolean;
}

export interface FileUploadedAlert {
    id: string;
    fileOwner: string;
    fileName: string;
}
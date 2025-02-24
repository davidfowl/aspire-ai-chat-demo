import { streamJsonValues } from './stream';
import { Chat, Message, MessageFragment } from '../types/ChatTypes';

class ChatService {
	backendUrl: string;

	constructor(backendUrl: string) {
		this.backendUrl = backendUrl;
	}

	async getChats(): Promise<Chat[]> {
		const response = await fetch(`${this.backendUrl}`);
		if (!response.ok) {
			throw new Error('Error fetching chats');
		}
		return await response.json();
	}

	async getChatMessages(chatId: string): Promise<Message[]> {
		const response = await fetch(`${this.backendUrl}/${chatId}`);
		if (!response.ok) {
			throw new Error('Error fetching chat messages');
		}
		return await response.json();
	}

	async createChat(name: string): Promise<Chat> {
		const response = await fetch(`${this.backendUrl}`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json'
			},
			body: JSON.stringify({ name })
		});
		if (!response.ok) {
			throw new Error('Failed to create chat');
		}
		return await response.json();
	}

	async *stream(id: string, lastMessageId: string, lastFragmentId: string, abortController: AbortController): AsyncGenerator<MessageFragment> {
		const response = await fetch(`${this.backendUrl}/stream/${id}`, {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify({ lastMessageId, lastFragmentId }),
			signal: abortController.signal
		});
		if (!response.ok) {
			throw new Error('Error fetching chat stream');
		}

		if (!response.body) {
			throw new Error('ReadableStream not supported in this browser.');
		}

		for await (const value of streamJsonValues(response, abortController.signal)) {
			yield { 
				id: value.id, 
				sender: value.sender, 
				text: value.text, 
				isFinal: value.isFinal ?? false, 
				fragmentId: value.fragmentId
			};
		}

		console.debug(`Stream ended for chat: ${id}`);
	}

	async sendPrompt(id: string, prompt: string): Promise<void> {
		const response = await fetch(`${this.backendUrl}/${id}`, {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify({ text: prompt })
		});

		if (!response.ok) {
			let errorMessage;
			try {
				errorMessage = await response.text();
			} catch (e) {
				errorMessage = response.statusText;
			}
			throw new Error(`Error sending prompt: ${errorMessage}`);
		}
	}

	async deleteChat(id: string): Promise<void> {
		const response = await fetch(`${this.backendUrl}/${id}`, {
			method: 'DELETE'
		});

		if (!response.ok) {
			throw new Error('Failed to delete chat');
		}
	}

	async cancelChat(id: string): Promise<void> {
		const response = await fetch(`${this.backendUrl}/${id}/cancel`, {
			method: 'POST'
		});
		if (!response.ok) {
			throw new Error('Failed to cancel chat');
		}
	}
}

export default ChatService;

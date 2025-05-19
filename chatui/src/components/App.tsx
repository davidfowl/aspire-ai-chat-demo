import React, { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import ChatService from '../services/ChatService';
import { Message, Chat } from '../types/ChatTypes';
import Sidebar from './Sidebar';
import ChatContainer from './ChatContainer';
import VirtualizedChatList from './VirtualizedChatList';
import './App.css';
import { nanoid } from 'nanoid';          // lightweight id helper (already in many React projects; falls back to simple Date.now() if not installed)

const loadingIndicatorId = 'loading-indicator';

interface ChatParams {
    chatId?: string;
    [key: string]: string | undefined;
}

const App: React.FC = () => {
    const [messages, setMessages] = useState<Message[]>([]);
    const [prompt, setPrompt] = useState<string>('');
    const [chats, setChats] = useState<Chat[]>([]);
    const [selectedChatId, setSelectedChatId] = useState<string | null>(null);
    const selectedChatIdRef = useRef<string | number | null>(null);
    const [loadingChats, setLoadingChats] = useState<boolean>(true);
    const messagesEndRef = useRef<HTMLDivElement>(null);
    const [newChatName, setNewChatName] = useState<string>('');
    const abortControllerRef = useRef<AbortController | null>(null);
    const [shouldAutoScroll, setShouldAutoScroll] = useState<boolean>(true);
    const [streamingMessageId, setStreamingMessageId] = useState<string | null>(null);
    const { chatId } = useParams<ChatParams>();
    const navigate = useNavigate();

    const chatService = useMemo(() => ChatService.getInstance('/api/chat'), []);

    useEffect(() => {
        const fetchChats = async () => {
            try {
                const data = await chatService.getChats();
                setChats(data);
            } catch (error) {
                console.error('Error fetching chats:', error);
            } finally {
                setLoadingChats(false);
            }
        };

        fetchChats();

        if (chatId) {
            handleChatSelect(chatId);
        }
    }, [chatId, chatService]);

    const onSelectChat = useCallback((id: string) => {
        navigate(`/chat/${id}`);
    }, [navigate]);

    const scrollToBottom = useCallback((behavior: ScrollBehavior = 'smooth') => {
        if (messagesEndRef.current && shouldAutoScroll) {
            messagesEndRef.current.scrollTo({
                top: messagesEndRef.current.scrollHeight,
                behavior
            });
        }
    }, [shouldAutoScroll]);

    const handleChatSelect = useCallback(async (id: string) => {
        setSelectedChatId(id);
        selectedChatIdRef.current = id;
        setMessages([]);
        let lastMessageId: string | null = null;

        try {
            const messages = await chatService.getChatMessages(id);
            const filteredMessages = messages.filter(msg => msg.text);
            lastMessageId = filteredMessages.length > 0 ? filteredMessages[filteredMessages.length - 1].id : null;
            setMessages(messages);
            setTimeout(() => scrollToBottom('instant'), 100);
        } catch (error) {
            console.error('Error fetching chat messages:', error);
        }

        if (abortControllerRef.current) {
            abortControllerRef.current.abort();
        }
        const abortController = new AbortController();
        abortControllerRef.current = abortController;

        let chatId = id;

        (async () => {
            try {
                console.log('streamChat started:', id);
                const stream = chatService.stream(id, lastMessageId, abortController);
                for await (const { id, sender, text, isFinal } of stream) {
                    if (selectedChatIdRef.current !== chatId) {
                        break;
                    }
                    if (isFinal) {
                        setStreamingMessageId(null);
                        if (!text) continue;
                    } else {
                        setStreamingMessageId(current => current ? current : id);
                    }
                    updateMessageById(id, text, sender, isFinal);
                }
            } catch (error) {
                // ...existing error handling...
                console.error('Stream error:', error);
            } finally {
                console.log('streamChat finished:', id);
                setStreamingMessageId(null);
            }
        })();
    }, [chatService, scrollToBottom]);

    const updateMessageById = (id: string, newText: string, sender: string, isFinal: boolean = false) => {
        const generatingReplyMessageText = 'Generating reply...';

        function getMessageText(existingText: string, newText: string): string {
            // If the existing text is the same as the generating reply message text, replace it with the new text 
            if (existingText === generatingReplyMessageText) {
                return newText;
            }

            // If the existing text starts with the generating reply message text, replace it with the new text
            if (existingText.startsWith(generatingReplyMessageText)) {
                return existingText.replace(generatingReplyMessageText, '') + newText;
            }

            // Otherwise, append the new text to the existing text
            return existingText + newText;
        }

        setMessages(prevMessages => {
            const lastUserMessage = prevMessages.filter(m => m.sender === 'user').slice(-1)[0];
            if (isFinal && lastUserMessage && lastUserMessage.text === newText) {
                return prevMessages;
            }
            const existingMessage = prevMessages.find(msg => msg.id === id);
            if (existingMessage) {
                return prevMessages.map(msg =>
                    msg.id === id
                        ? {
                            ...msg,
                            text: getMessageText(msg.text, newText),
                            isLoading: false,
                            sender: sender || msg.sender
                        }
                        : msg
                );
            } else {
                return [...prevMessages.filter(msg => msg.id !== loadingIndicatorId),
                { id, sender, text: newText, isLoading: false },
                ];
            }
        });
    };

    const handleScroll = useCallback((e: React.UIEvent<HTMLDivElement>) => {
        const container = e.target as HTMLDivElement;
        const isNearBottom = container.scrollHeight - container.scrollTop - container.clientHeight < 100;
        setShouldAutoScroll(isNearBottom);
    }, []);

    useEffect(() => {
        const container = messagesEndRef.current;
        if (container) {
            container.addEventListener('scroll', handleScroll as unknown as EventListener);
            return () => container.removeEventListener('scroll', handleScroll as unknown as EventListener);
        }
    }, [handleScroll]);

    useEffect(() => {
        if (shouldAutoScroll) {
            scrollToBottom();
        }
    }, [messages, scrollToBottom]);

    const handleNewChat = useCallback(() => {
        setSelectedChatId(null);
        setMessages([]);
        navigate('/');
    }, [navigate]);

    const generateChatTitle = (text: string): string => {
        // Take first 40 chars of first line, or "New Chat" if empty
        const firstLine = text.split('\n')[0].trim();
        return firstLine.length > 40 ? firstLine.substring(0, 37) + '...' : firstLine || 'New Chat';
    };

    const handleSubmit = useCallback(async (e: React.FormEvent) => {
        e.preventDefault();
        if (!prompt.trim() || streamingMessageId) return;

        const userMessage = { id: nanoid(), sender: 'user', text: prompt } as Message;
        setMessages(prev => [...prev, userMessage]);

        setMessages(prev => [
            ...prev,
            { id: loadingIndicatorId, sender: 'assistant', text: 'Generating reply...' }
        ]);

        try {
            let activeChatId = selectedChatId;
            if (!activeChatId) {
                const title = generateChatTitle(prompt);
                const newChat = await chatService.createChat(title);
                setChats(prev => [...prev, newChat]);
                activeChatId = newChat.id;
                setSelectedChatId(activeChatId);
                navigate(`/chat/${activeChatId}`);
            }

            await chatService.sendPrompt(activeChatId!, prompt);
            setPrompt('');
        } catch (error) {
            console.error('handleSubmit error:', error);
            setMessages(prev =>
                prev.map(msg =>
                    msg.id === loadingIndicatorId
                        ? { ...msg, text: '[Error in receiving response]' }
                        : msg
                )
            );
        }
    }, [prompt, selectedChatId, streamingMessageId, chatService, navigate]);

    const handleNewChatSubmit = useCallback(async (e: React.FormEvent) => {
        e.preventDefault();
        if (!newChatName.trim()) return;

        try {
            const newChat = await chatService.createChat(newChatName);
            setChats(prevChats => [...prevChats, newChat]);
            setNewChatName('');
            onSelectChat(newChat.id);
        } catch (error) {
            console.error('handleNewChatSubmit error:', error);
        }
    }, [newChatName, chatService, onSelectChat]);

    const handleDeleteChat = async (e: React.MouseEvent, chatId: string) => {
        e.stopPropagation();
        try {
            await chatService.deleteChat(chatId);
            setChats(prevChats => prevChats.filter(chat => chat.id !== chatId));
            if (selectedChatId === chatId) {
                setSelectedChatId(null);
                setMessages([]);
            }
        } catch (error) {
            console.error('handleDeleteChat error:', chatId, error);
        }
    };

    const cancelChat = () => {
        if (!streamingMessageId) return;
        chatService.cancelChat(streamingMessageId);
    };

    return (
        <div className="app-container">
            <Sidebar
                chats={chats}
                selectedChatId={selectedChatId}
                loadingChats={loadingChats}
                handleDeleteChat={handleDeleteChat}
                onNewChat={handleNewChat}
            />
            <ChatContainer
                messages={messages}
                prompt={prompt}
                setPrompt={setPrompt}
                handleSubmit={handleSubmit}
                cancelChat={cancelChat}
                streamingMessageId={streamingMessageId}
                messagesEndRef={messagesEndRef}
                shouldAutoScroll={shouldAutoScroll}
                renderMessages={() => (
                    <VirtualizedChatList messages={messages} />
                )}
            />
        </div>
    );
};

export default App;

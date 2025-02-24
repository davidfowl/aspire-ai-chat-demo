import React, { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import ChatService from '../services/ChatService';
import Sidebar from './Sidebar';
import ChatContainer from './ChatContainer';
import VirtualizedChatList from './VirtualizedChatList';
import './App.css';

const loadingIndicatorId = 'loading-indicator';

const App = () => {
    const [messages, setMessages] = useState([]);
    const [prompt, setPrompt] = useState('');
    const [chats, setChats] = useState([]);
    const [selectedChatId, setSelectedChatId] = useState(null);
    const selectedChatIdRef = useRef(null);
    const [loadingChats, setLoadingChats] = useState(true);
    const messagesEndRef = useRef(null);
    const [newChatName, setNewChatName] = useState('');
    const abortControllerRef = useRef(null);
    const [shouldAutoScroll, setShouldAutoScroll] = useState(true);
    const [streamingMessageId, setStreamingMessageId] = useState(null);
    const { chatId } = useParams();
    const navigate = useNavigate();

    const chatService = useMemo(() => new ChatService('/api/chat'), []);

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
    }, [chatId, chatService]);

    useEffect(() => {
        if (chatId) {
            handleChatSelect(chatId);
        }
    }, [chatId]);

    const onSelectChat = useCallback((id) => {
        navigate(`/chat/${id}`);
    }, [navigate]);

    const scrollToBottom = useCallback((behavior = 'smooth') => {
        if (messagesEndRef.current && shouldAutoScroll) {
            messagesEndRef.current.scrollTo({
                top: messagesEndRef.current.scrollHeight,
                behavior
            });
        }
    }, [shouldAutoScroll]);

    const handleChatSelect = useCallback(async (id) => {
        setSelectedChatId(id);
        selectedChatIdRef.current = id;
        // Clear the message list immediately on chat switch
        setMessages([]);
        let lastMessageId = null;
        let lastFragmentId = null;

        try {
            const data = await chatService.getChatMessages(id);
            const filteredMessages = data.filter(msg => msg.text);
            const lastMessage = filteredMessages.length > 0 ? filteredMessages[filteredMessages.length - 1] : null;
            lastMessageId = lastMessage ? lastMessage.id : null;

            setMessages(data);
            setTimeout(() => scrollToBottom('instant'), 100);
        } catch (error) {
            console.error('Error fetching chat messages:', error);
        }

        const streamChat = async (chatId) => {
            if (abortControllerRef.current) {
                abortControllerRef.current.abort();
            }

            let abortController = new AbortController();
            abortControllerRef.current = abortController;
            const currentChatId = chatId;

            // Stream messages while the chat is selected and not aborted
            while (abortController.signal.aborted === false &&
                currentChatId === selectedChatIdRef.current) {

                console.log('streamChat started:', chatId);

                try {
                    const stream = chatService.stream(chatId, lastMessageId, lastFragmentId, abortController);
                    for await (const { id, sender, text, isFinal, fragmentId } of stream) {
                        if (selectedChatIdRef.current !== currentChatId) {
                            break;
                        }

                        console.debug('Received chunk:', id, sender, text, isFinal, lastFragmentId);

                        // Update lastFragmentId for the next chunk
                        lastFragmentId = fragmentId;

                        if (isFinal) {
                            // Reset lastMessageId and lastFragmentId when the message is final
                            lastMessageId = id;
                            setStreamingMessageId(null);

                            // If the message is empty, skip it
                            if (!text) continue;

                        } else {
                            setStreamingMessageId(current => current ? current : id);
                        }

                        updateMessageById(id, text, sender, isFinal);
                    }
                } catch (error) {
                    console.debug('streamChat error:', chatId, error);

                    if (error.name !== 'AbortError') {
                        console.error('Streaming error:', error);
                        setMessages(prev =>
                            prev.map(msg =>
                                msg.id === loadingIndicatorId ? { ...msg, text: '[Error in receiving response]', isLoading: false } : msg
                            )
                        );
                    }
                }
                finally {
                    console.log('streamChat finished:', chatId);
                    setStreamingMessageId(null);
                }
            }
        };

        streamChat(id);
    }, [chatService, scrollToBottom]);

    const updateMessageById = (id, newText, sender, isFinal = false) => {
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
                            text: (msg.text === 'Generating reply...' ? newText : msg.text + newText),
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

    const handleScroll = useCallback((e) => {
        const container = e.target;
        const isNearBottom = container.scrollHeight - container.scrollTop - container.clientHeight < 100;
        setShouldAutoScroll(isNearBottom);
    }, []);

    useEffect(() => {
        const container = messagesEndRef.current;
        if (container) {
            container.addEventListener('scroll', handleScroll);
            return () => container.removeEventListener('scroll', handleScroll);
        }
    }, [handleScroll]);

    useEffect(() => {
        if (shouldAutoScroll) {
            scrollToBottom();
        }
    }, [messages, scrollToBottom]);

    const handleSubmit = useCallback(async (e) => {
        e.preventDefault();
        if (!prompt.trim() || !selectedChatId) return;
        if (streamingMessageId) return;

        const userMessage = { id: Date.now(), sender: 'user', text: prompt };
        setMessages(prevMessages => [...prevMessages, userMessage]);

        setMessages(prevMessages => [
            ...prevMessages,
            { id: loadingIndicatorId, sender: 'assistant', text: 'Generating reply...', isLoading: true }
        ]);

        try {
            chatService.sendPrompt(selectedChatId, prompt);
            setPrompt('');
        } catch (error) {
            console.error('handleSubmit error:', error);
            setMessages(prev =>
                prev.map(msg =>
                    msg.id === loadingIndicatorId ? { ...msg, text: '[Error in receiving response]', isLoading: false } : msg
                )
            );
        }
    }, [prompt, selectedChatId, streamingMessageId, chatService]);

    const handleNewChatSubmit = useCallback(async (e) => {
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

    const handleDeleteChat = async (e, chatId) => {
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
        chatService.cancelChat(streamingMessageId);
    };

    return (
        <div className="app-container">
            <Sidebar
                chats={chats}
                selectedChatId={selectedChatId}
                loadingChats={loadingChats}
                newChatName={newChatName}
                setNewChatName={setNewChatName}
                handleNewChatSubmit={handleNewChatSubmit}
                handleDeleteChat={handleDeleteChat}
                onSelectChat={onSelectChat}
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
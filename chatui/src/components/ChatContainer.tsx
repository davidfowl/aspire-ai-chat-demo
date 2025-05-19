import React, { useEffect, ReactNode, RefObject } from 'react';
import ReactMarkdown from 'react-markdown';
import { Message } from '../types/ChatTypes';

interface ChatContainerProps {
    messages: Message[];
    prompt: string;
    setPrompt: (prompt: string) => void;
    handleSubmit: (e: React.FormEvent) => void;
    cancelChat: () => void;
    streamingMessageId: string | null;
    messagesEndRef: RefObject<HTMLDivElement | null>;
    shouldAutoScroll: boolean;
    renderMessages: () => ReactNode;
}

const ChatContainer: React.FC<ChatContainerProps> = ({
    messages,
    prompt,
    setPrompt,
    handleSubmit,
    cancelChat,
    streamingMessageId,
    messagesEndRef,
    shouldAutoScroll,
    renderMessages
}: ChatContainerProps) => {
    // Scroll only if near the bottom
    useEffect(() => {
        if (shouldAutoScroll && messagesEndRef.current) {
            messagesEndRef.current.scrollIntoView({ behavior: 'smooth' });
        }
    }, [messages, shouldAutoScroll, messagesEndRef]);

    return (
        <div className="chat-container">
            <div ref={messagesEndRef} className="messages-container">
                {messages.length === 0 && (
                    <div className="landing-content">
                        <h1>Chat with AI</h1>
                        <div className="examples-grid">
                            <div className="example-card">
                                <h3>💡 Examples</h3>
                                <ul>
                                    <li>"Explain quantum computing in simple terms"</li>
                                    <li>"Got any creative ideas for a 10 year old's birthday?"</li>
                                    <li>"How do I make an HTTP request in Javascript?"</li>
                                </ul>
                            </div>
                        </div>
                    </div>
                )}
                {messages.map(msg => (
                    <div key={msg.id} className={`message ${msg.sender}`}>
                        <div className="message-content">
                            <ReactMarkdown>{msg.text}</ReactMarkdown>
                        </div>
                    </div>
                ))}
            </div>
            <div className="message-form">
                <form onSubmit={handleSubmit}>
                    <input
                        type="text"
                        value={prompt}
                        onChange={e => setPrompt(e.target.value)}
                        placeholder="Send a message..."
                        disabled={streamingMessageId ? true : false}
                        className="message-input"
                    />
                    {streamingMessageId ? (
                        <button type="button" onClick={cancelChat} className="message-button">
                            Stop
                        </button>
                    ) : (
                        <button type="submit" disabled={streamingMessageId ? true : false} className="message-button">
                            Send
                        </button>
                    )}
                </form>
            </div>
        </div>
    );
};

export default ChatContainer;

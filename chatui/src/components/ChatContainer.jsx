import React, { useEffect } from 'react';
import ReactMarkdown from 'react-markdown';

const ChatContainer = ({
    messages,
    prompt,
    setPrompt,
    handleSubmit,
    cancelChat,
    streamingMessageId,
    messagesEndRef, // already received as a prop
    shouldAutoScroll // new prop
}) => {
    // Scroll only if near the bottom
    useEffect(() => {
        if (shouldAutoScroll && messagesEndRef.current) {
            messagesEndRef.current.scrollIntoView({ behavior: 'smooth' });
        }
    }, [messages, shouldAutoScroll, messagesEndRef]);

    return (
        <div className="chat-container">
            <div ref={messagesEndRef} className="messages-container">
                {messages.map(msg => (
                    <div key={msg.id} className={`message ${msg.sender}`}>
                        <div className="message-content">
                            <ReactMarkdown>{msg.text}</ReactMarkdown>
                        </div>
                    </div>
                ))}
            </div>
            <form onSubmit={handleSubmit} className="message-form">
                <input
                    type="text"
                    value={prompt}
                    onChange={e => setPrompt(e.target.value)}
                    placeholder="Enter your message..."
                    disabled={streamingMessageId}
                    className="message-input"
                />
                {streamingMessageId ? (
                    <button type="button" onClick={cancelChat} className="message-button">
                        Stop
                    </button>
                ) : (
                    <button type="submit" disabled={streamingMessageId} className="message-button">
                        Send
                    </button>
                )}
            </form>
        </div>
    );
};

export default ChatContainer;

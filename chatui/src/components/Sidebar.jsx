import React from 'react';
import { useNavigate } from 'react-router-dom';

const Sidebar = ({
    chats,
    selectedChatId,
    loadingChats,
    newChatName,
    setNewChatName,
    handleNewChatSubmit,
    handleDeleteChat
}) => {
    const navigate = useNavigate();
    return (
        <div className="sidebar">
            <div className="sidebar-header">
                <h2>Chats</h2>
                {loadingChats && <p>Loading...</p>}
            </div>
            <ul className="chat-list">
                {chats.map(chat => (
                    <li
                        key={chat.id}
                        onClick={() => navigate(`/chat/${chat.id}`)}
                        className={`chat-item ${selectedChatId === chat.id ? 'selected' : ''}`}
                    >
                        <span className="chat-name">{chat.name}</span>
                        <button
                            className="delete-chat-button"
                            onClick={(e) => handleDeleteChat(e, chat.id)}
                            title="Delete chat"
                        >
                            ×
                        </button>
                    </li>
                ))}
            </ul>
            <form onSubmit={handleNewChatSubmit} className="new-chat-form">
                <input
                    type="text"
                    value={newChatName}
                    onChange={e => setNewChatName(e.target.value)}
                    placeholder="New chat name"
                    className="new-chat-input"
                />
                <button type="submit" className="new-chat-button">
                    Create Chat
                </button>
            </form>
        </div>
    );
};

export default Sidebar;

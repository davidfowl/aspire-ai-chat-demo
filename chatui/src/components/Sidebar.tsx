import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Chat } from '../types/ChatTypes';

interface SidebarProps {
    chats: Chat[];
    selectedChatId: string | null;
    loadingChats: boolean;
    handleDeleteChat: (e: React.MouseEvent, chatId: string) => void;
    onNewChat: () => void;
}

const Sidebar: React.FC<SidebarProps> = ({
    chats,
    selectedChatId,
    loadingChats,
    handleDeleteChat,
    onNewChat
}) => {
    const navigate = useNavigate();
    
    return (
        <div className="sidebar">
            <div className="sidebar-header">
                <h2>Chats</h2>
                {loadingChats && <p>Loading...</p>}
            </div>
            <button onClick={onNewChat} className="new-chat-button sidebar-new-chat">
                + New chat
            </button>
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
        </div>
    );
};

export default Sidebar;

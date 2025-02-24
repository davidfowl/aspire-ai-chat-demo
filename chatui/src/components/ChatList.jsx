import { useNavigate, useParams } from 'react-router-dom';

function ChatList({ chats, selectedChat, setSelectedChat }) {
    const navigate = useNavigate();
    const params = useParams();

    return (
        <div>
            {
                chats.map(chat => (
                    <button
                        key={chat.id}
                        onClick={() => {
                            setSelectedChat(chat);
                            if (chat.id !== params.chatId) {
                                navigate(`/chat/${chat.id}`);
                            }
                        }}
                    >
                        {chat.name}
                    </button>
                ))
            }
        </div>
    );
}

export default ChatList;

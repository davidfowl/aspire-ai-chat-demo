import React from 'react';
import { FixedSizeList as List } from 'react-window';

function VirtualizedChatList({ messages }) {
  return (
    <List
      height={600}
      width={'100%'}
      itemCount={messages.length}
      itemSize={60}
    >
      {({ index, style }) => {
        const msg = messages[index];
        return (
          <div style={style} key={msg.id}>
            <strong>{msg.sender}:</strong> {msg.text}
          </div>
        );
      }}
    </List>
  );
}

export default VirtualizedChatList;

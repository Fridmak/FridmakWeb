export async function sendMessageToServer(text) {
    const response = await fetch('/Chat/SendMessage', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Text: text })
    });
    return await response.json();
}

export async function getMessagesFromServer(url) {
    const res = await fetch(url);
    const data = await res.json();
    
    return data;
}

export async function sendEditMessageToServer(messageId, newMessage, comment, isDelete) {
    const response = await fetch('/Chat/EditMessage', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Id: messageId, Message: newMessage, Comment: comment, Delete: isDelete })
    });
    return await response.json();
}
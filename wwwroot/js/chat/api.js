export async function sendMessageToServer(text) {
    const response = await fetch('/Chat/SendMessage', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Text: text })
    });

    return response.json();
}
export async function getMessagesFromServer() {
    const res = await fetch('/Chat/GetMessages');
    return await res.json();
}

export async function sendEditMessageToServer(messageId, newMessage, comment, isDelete) {
    const response = await fetch('/Chat/EditMessage', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Id: messageId , Message: newMessage, Comment: comment, Delete: isDelete})
    });

    return response.json();
}
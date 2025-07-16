export async function sendMessageToServer(text) {
    await fetch('/User/SendMessage', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            MessageText: text,
            SenderId: 1
        })
    });
}

export async function getMessagesFromServer() {
    const res = await fetch('/User/GetMessages');
    return await res.json();
}
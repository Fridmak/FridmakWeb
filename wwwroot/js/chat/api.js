export async function sendMessageToServer(text) {
    await fetch('/Chat/SendMessage', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Text: text })
    });
}
export async function getMessagesFromServer() {
    const res = await fetch('/Chat/GetMessages');
    return await res.json();
}
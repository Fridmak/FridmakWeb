export async function sendMessageToServer(user, text) {
    await fetch('/User/SendMessage', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Text: text , UserName : user})
    });
}
export async function getMessagesFromServer() {
    const res = await fetch('/User/GetMessages');
    return await res.json();
}
function getAntiForgeryToken() {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    return token ? { 'RequestVerificationToken': token } : {};
}

export async function sendMessageToServer(text) {
    const response = await fetch('/Chat/SendMessage', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...getAntiForgeryToken() },
        body: JSON.stringify({ Text: text })
    });
    return await response.json();
}

export async function getMessagesFromServer(url) {
    const res = await fetch(url);

    if (res.redirected || !res.ok) {
        window.location.href = '/User/Login?ReturnUrl=%2FChat';
        throw new Error('Authentication required');
    }

    return await res.json();
}

export async function sendEditMessageToServer(messageId, newMessage, comment, isDelete) {
    const response = await fetch('/Chat/EditMessage', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...getAntiForgeryToken() },
        body: JSON.stringify({ Id: messageId, Message: newMessage, Comment: comment, Delete: isDelete })
    });
    return await response.json();
}

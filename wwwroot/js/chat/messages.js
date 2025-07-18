export function updateMessageList(chatContainer, messages) {
    const wasAtBottom =
        chatContainer.scrollHeight - chatContainer.clientHeight <= chatContainer.scrollTop + 1;

    const messageList = chatContainer.querySelector('#chat-messages');
    messageList.innerHTML = '';

    messages.forEach(msg => {
        const li = document.createElement('li');
        li.classList.add('chat-message');

        const date = new Date(msg.timestamp);
        const formattedDate = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')} ${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;

        li.innerHTML = `
            <strong>${msg.userName}</strong>
            <small> (${formattedDate})</small>:
            <p>${msg.text}</p>
        `;

        messageList.appendChild(li);
    });

    const atBottom = wasAtBottom ||
        chatContainer.scrollHeight - chatContainer.clientHeight <= 1;

    if (atBottom) {
        scrollDownSmooth(chatContainer);
    }
}

export function scrollDown(chatContainer) {
    chatContainer.scrollTop = chatContainer.scrollHeight;
}
export function scrollDownSmooth(chatContainer) {
    chatContainer.scrollTo({
        top: chatContainer.scrollHeight,
        behavior: 'smooth'
    });
}
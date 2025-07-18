export function updateMessageList(messageList, messages) {
    messageList.innerHTML = '';

    messages.forEach(msg => {
        const li = document.createElement('li');
        li.classList.add('chat-message');

        // Форматируем дату вручную
        const date = new Date(msg.timestamp);
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0'); // Месяцы с 0
        const day = String(date.getDate()).padStart(2, '0');
        const hours = String(date.getHours()).padStart(2, '0');
        const minutes = String(date.getMinutes()).padStart(2, '0');

        const formattedDate = `${year}-${month}-${day} ${hours}:${minutes}`;

        li.innerHTML = `
            <strong>${msg.userName}</strong>
            <small> (${formattedDate})</small>:
            <p>${msg.text}</p>
        `;

        messageList.appendChild(li);
    });

    messageList.scrollTop = messageList.scrollHeight;
}
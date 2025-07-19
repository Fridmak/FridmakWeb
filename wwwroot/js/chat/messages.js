export function updateMessageList(chatContainer, messages) {
    const messageList = chatContainer.querySelector('#chat-messages');
    const wasAtBottom = isAtBottom(chatContainer);

    messageList.innerHTML = '';

    messages.forEach(msg => {
        const li = document.createElement('li');
        li.classList.add('chat-message');

        const date = new Date(msg.timestamp);
        const formattedDate = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')} ${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;

        const div = document.createElement('div');
        div.classList.add('message-content');
        div.dataset.id = msg.messageId;

        const strong = document.createElement('strong');
        strong.textContent = msg.userName;

        const small = document.createElement('small');
        small.textContent = ` (${formattedDate}):`;

        const p = document.createElement('p');
        p.textContent = msg.text;

        div.appendChild(strong);
        div.appendChild(small);
        div.appendChild(document.createTextNode('\u00A0'));
        div.appendChild(p);

        if (isAdmin) {
            const btn = document.createElement('button');
            btn.classList.add('message-options');
            btn.title = 'Действия';
            btn.textContent = '⋯';

            btn.addEventListener('click', () => {
                console.log('Клик по кнопке действий', msg);
            });

            div.appendChild(btn);
        }

        li.appendChild(div);
        messageList.appendChild(li);
    });

    if (wasAtBottom) {
        scrollDownSmooth(chatContainer);
    }
}
export function isAtBottom(element) {
    const atBottom = element.scrollHeight - element.scrollTop - element.clientHeight <= 10;
    return atBottom;
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
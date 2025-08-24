export function updateMessageList(chatContainer, messages, isFullReload = false) {
    const messageList = chatContainer.querySelector('#chat-messages');
    const wasAtBottom = isAtBottom(chatContainer);

    if (isFullReload) {
        messageList.innerHTML = '';
        messages.forEach(item => {
            const [msg, action] = item;

            if (action === 'Send') {
                if (!msg || typeof msg !== 'object') {
                    console.warn('Некорректное сообщение:', msg);
                    return;
                }
                appendMessage(messageList, msg);
            }
        });
    } else {
        messages.forEach(item => {
            const [msg, action] = item;

            const existingMessage = messageList.querySelector(`[data-id="${msg.messageId}"]`)?.parentElement;

            switch (action) {
                case 'Send':
                    appendMessage(messageList, msg);
                    break;

                case 'Edit':
                    if (existingMessage) {
                        const p = existingMessage.querySelector('p');
                        if (p) p.textContent = msg.text;
                    }
                    break;

                case 'Delete':
                    if (existingMessage) {
                        existingMessage.remove();
                    }
                    break;

                default:
                    console.warn('Неизвестное действие:', action);
            }
        });
    }

    if (wasAtBottom) {
        scrollDownSmooth(chatContainer);
    }
}

function appendMessage(messageList, msg) {
    const li = document.createElement('li');
    li.classList.add('chat-message');

    const date = new Date(msg.timestamp);
    const formattedDate = formatLocalDate(msg.timestamp);

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

    if (window.isAdmin) {
        const btn = document.createElement('button');
        btn.classList.add('message-options');
        btn.title = 'Действия';
        btn.textContent = '⋯';

        div.appendChild(btn);
    }

    li.appendChild(div);
    messageList.appendChild(li);
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

function formatLocalDate(timestamp) {
    const date = new Date(timestamp);

    return new Intl.DateTimeFormat('ru-RU', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    }).format(date);
}
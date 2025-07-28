export function updateMessageList(chatContainer, messages, isFullReload = false) {
    const messageList = chatContainer.querySelector('#chat-messages');
    const wasAtBottom = isAtBottom(chatContainer);

    if (isFullReload) {
        messageList.innerHTML = '';
        messages.forEach(([msg, action]) => {
            if (action === 'Send') {
                appendMessage(messageList, msg);
            }
        });
    } else {
        messages.forEach(([msg, action]) => {
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

    if (window.isAdmin) {
        const btn = document.createElement('button');
        btn.classList.add('message-options');
        btn.title = 'Действия';
        btn.textContent = '⋯';

        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            console.log('Клик по кнопке действий', msg);
        });

        div.appendChild(btn);
    }

    li.appendChild(div);
    messageList.appendChild(li);
}
const thread = document.getElementById('message-thread');
const list = document.getElementById('message-list');
const form = document.getElementById('message-form');
const input = document.getElementById('message-input');
const token = form?.querySelector('input[name="__RequestVerificationToken"]')?.value;

if (thread && list && form && input) {
    const conversationId = Number(thread.dataset.conversationId);
    const currentUserId = Number(thread.dataset.currentUserId);
    let lastId = Number(thread.dataset.lastMessageId || 0);

    function scrollToBottom() {
        list.scrollTop = list.scrollHeight;
    }

    function appendMessage(message) {
        if (!message || document.querySelector(`[data-id="${message.id}"]`)) {
            return;
        }

        const row = document.createElement('li');
        row.className = `message-row ${message.senderId === currentUserId ? 'mine' : 'theirs'}`;
        row.dataset.id = message.id;

        const bubble = document.createElement('div');
        bubble.className = 'bubble';

        const text = document.createElement('div');
        text.className = 'text';
        text.textContent = message.text;

        const meta = document.createElement('div');
        meta.className = 'meta';
        meta.textContent = new Date(message.sentAt).toLocaleTimeString([], {
            hour: '2-digit',
            minute: '2-digit'
        });

        bubble.append(text, meta);
        row.append(bubble);
        list.append(row);

        lastId = Math.max(lastId, message.id);
    }

    async function markRead() {
        try {
            await fetch('/Messages/MarkRead', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({ conversationId })
            });
        } catch {
            // Polling will retry on the next tick.
        }
    }

    async function poll() {
        try {
            const response = await fetch(
                `/Messages/GetMessages?conversationId=${conversationId}&afterId=${lastId}`);

            if (!response.ok) {
                return;
            }

            const messages = await response.json();
            if (messages.length > 0) {
                messages.forEach(appendMessage);
                scrollToBottom();
                await markRead();
            }
        } catch {
            // Keep polling; temporary network errors are expected.
        }
    }

    form.addEventListener('submit', async (event) => {
        event.preventDefault();

        const text = input.value.trim();
        if (!text) {
            return;
        }

        const button = form.querySelector('button[type="submit"]');
        button.disabled = true;

        try {
            const response = await fetch('/Messages/Send', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({ conversationId, text })
            });

            if (!response.ok) {
                const error = await response.json().catch(() => ({}));
                alert(error.error || 'Не удалось отправить сообщение.');
                return;
            }

            const message = await response.json();
            appendMessage(message);
            input.value = '';
            scrollToBottom();
        } finally {
            button.disabled = false;
        }
    });

    scrollToBottom();
    markRead();
    setInterval(poll, 2000);
}

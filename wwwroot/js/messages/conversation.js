const thread = document.getElementById('message-thread');
const list = document.getElementById('message-list');
const form = document.getElementById('message-form');
const input = document.getElementById('message-input');
const statusBadge = document.getElementById('connection-status');
const token = form?.querySelector('input[name="__RequestVerificationToken"]')?.value;

if (thread && list && form && input && window.signalR) {
    const conversationId = Number(thread.dataset.conversationId);
    const currentUserId = Number(thread.dataset.currentUserId);
    let lastId = Number(thread.dataset.lastMessageId || 0);

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/messages')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    function setStatus(text, cssClass) {
        if (!statusBadge) {
            return;
        }

        statusBadge.textContent = text;
        statusBadge.className = `badge ${cssClass}`;
    }

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

    async function loadMissedMessages() {
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
            }
        } catch {
            // Polling/fallback will retry.
        }
    }

    async function markRead() {
        try {
            if (connection.state === signalR.HubConnectionState.Connected) {
                await connection.invoke('MarkRead', conversationId);
                return;
            }

            await fetch('/Messages/MarkRead', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({ conversationId })
            });
        } catch {
            // Non-critical.
        }
    }

    connection.on('MessageReceived', async (message) => {
        appendMessage(message);
        scrollToBottom();
        await markRead();
    });

    connection.on('ConversationRead', () => {
        // Read receipts can be rendered here later.
    });

    connection.on('ConversationUpdated', () => {
        // The thread page already receives MessageReceived via the conversation group.
    });

    connection.onreconnecting(() => setStatus('переподключение…', 'bg-warning text-dark'));
    connection.onreconnected(async () => {
        setStatus('онлайн', 'bg-success');
        await connection.invoke('JoinConversation', conversationId);
        await loadMissedMessages();
    });
    connection.onclose(() => setStatus('polling', 'bg-secondary'));

    async function start() {
        try {
            await connection.start();
            await connection.invoke('JoinConversation', conversationId);
            setStatus('онлайн', 'bg-success');
        } catch (error) {
            console.warn('SignalR connection failed, falling back to polling.', error);
            setStatus('polling', 'bg-secondary');
            await loadMissedMessages();
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
            if (connection.state === signalR.HubConnectionState.Connected) {
                await connection.invoke('SendMessage', conversationId, text);
            } else {
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

                appendMessage(await response.json());
                scrollToBottom();
            }

            input.value = '';
        } catch (error) {
            alert(error.message || 'Не удалось отправить сообщение.');
        } finally {
            button.disabled = false;
        }
    });

    scrollToBottom();
    markRead();
    start();

    // Safety net: if WebSocket is unavailable, keep the conversation usable.
    setInterval(async () => {
        if (connection.state !== signalR.HubConnectionState.Connected) {
            await loadMissedMessages();
        }
    }, 5000);
}

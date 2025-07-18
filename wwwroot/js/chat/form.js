export function setupChatForm(form, chatContainer, sendMessageFn, scrollDown) {
    form.addEventListener('submit', async (e) => {
        e.preventDefault();

        const input = document.getElementById('message');
        const text = input.value.trim();

        if (!text) return;

        const button = form.querySelector('button');
        button.disabled = true;

        try {
            await sendMessageFn(text);
            input.value = '';
        } finally {
            button.disabled = false;
        }

        scrollDown(chatContainer);
    });
}
export function setupChatForm(form, messageList, sendMessageFn, loadMessagesFn) {
    form.addEventListener('submit', async (e) => {
        e.preventDefault();

        const text = document.getElementById('message').value.trim();

        if (!text) return;

        await sendMessageFn(text);
        document.getElementById('message').value = '';
        await loadMessagesFn(messageList);
    });
}
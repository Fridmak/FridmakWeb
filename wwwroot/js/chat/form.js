export function setupChatForm(form, messageList, sendMessageFn, loadMessagesFn) {
    form.addEventListener('submit', async (e) => {
        e.preventDefault();

        const text = document.getElementById('message').value.trim();
        const user = document.getElementById('username').value.trim();

        if (!text || !user) return;

        await sendMessageFn(user, text);
        document.getElementById('message').value = '';
        await loadMessagesFn(messageList);
    });
}
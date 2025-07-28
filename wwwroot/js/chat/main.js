import { setupChatForm } from './form.js';
import { updateMessageList, scrollDownSmooth, isAtBottom } from './messages.js';
import { sendMessageToServer, getMessagesFromServer } from './api.js';
import { addEditToContainer } from './edit_message.js';

const form = document.getElementById('chat-form');
const chatContainer = document.querySelector('.chat-container');
const scrollButton = document.getElementById('scroll-down-btn');

async function sendMessage(text) {
    await sendMessageToServer(text);
    scrollDownSmooth(chatContainer);
}

async function loadMessages(isFullReload = false) {
    const url = `/Chat/GetMessages?loadOld=${isFullReload}`;
    const messages = await getMessagesFromServer(url);
    updateMessageList(chatContainer, messages, isFullReload);
}

function toggleScrollButtonVisibility() {
    scrollButton.style.display = isAtBottom(chatContainer) ? 'none' : 'inline-block';
}

async function initChat() {
    loadMessages(false);
    scrollDownSmooth(chatContainer);

    setInterval(async () => {
        await loadMessages(false);
    }, 500);
}

await initChat();

scrollButton.addEventListener('click', () => {
    scrollDownSmooth(chatContainer);
});

setInterval(toggleScrollButtonVisibility, 700);

setupChatForm(form, chatContainer, sendMessage, scrollDownSmooth);
addEditToContainer(chatContainer);
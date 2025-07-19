import { setupChatForm } from './form.js';
import { updateMessageList, scrollDown, scrollDownSmooth, isAtBottom } from './messages.js';
import { sendMessageToServer, getMessagesFromServer } from './api.js';
import { addEditToContainer } from './edit_message.js';

const form = document.getElementById('chat-form');
const chatContainer = document.querySelector('.chat-container');
const scrollButton = document.getElementById('scroll-down-btn');

async function sendMessage(text) {
    const newMessage = await sendMessageToServer(text);
    scrollDown(chatContainer);
}

async function loadMessages() {
    const messages = await getMessagesFromServer();
    updateMessageList(chatContainer, messages);
}

function toggleScrollButtonVisibility() {
    scrollButton.style.display = isAtBottom(chatContainer) ? 'none' : 'inline-block';
}

setupChatForm(form, chatContainer, sendMessage, scrollDownSmooth);

setInterval(loadMessages, 500);

await loadMessages();
scrollDown(chatContainer);

setInterval(toggleScrollButtonVisibility, 700);

scrollButton.addEventListener('click', () => {
    scrollDownSmooth(chatContainer);
});

addEditToContainer(chatContainer);
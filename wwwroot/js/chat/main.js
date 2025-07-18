import { setupChatForm } from './form.js';
import { updateMessageList, scrollDown, scrollDownSmooth } from './messages.js';
import { sendMessageToServer, getMessagesFromServer } from './api.js';

const form = document.getElementById('chat-form');
const chatContainer = document.querySelector('.chat-container');

async function sendMessage(text) {
    const newMessage = await sendMessageToServer(text);
    scrollDown(chatContainer);
}

async function loadMessages() {
    const messages = await getMessagesFromServer();
    updateMessageList(chatContainer, messages);
}

setupChatForm(form, chatContainer, sendMessage, scrollDownSmooth);

setInterval(loadMessages, 500);

await loadMessages();
scrollDown(chatContainer);
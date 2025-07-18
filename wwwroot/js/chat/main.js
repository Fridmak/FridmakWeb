import { setupChatForm } from './form.js';
import { updateMessageList, scrollDown, scrollDownSmooth } from './messages.js';
import { sendMessageToServer, getMessagesFromServer } from './api.js';

const form = document.getElementById('chat-form');
const chatContainer = document.querySelector('.chat-container');
const messageList = document.getElementById('chat-messages');

async function sendMessage(text) {
    await sendMessageToServer(text);
    scrollDownSmooth(chatContainer);
}

async function loadMessages() {
    const messages = await getMessagesFromServer();
    updateMessageList(chatContainer, messages);
}

setupChatForm(form, messageList, sendMessage, loadMessages);

setInterval(loadMessages, 1000);

scrollDown(chatContainer)
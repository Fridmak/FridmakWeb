import { setupChatForm } from './form.js';
import { updateMessageList } from './messages.js';
import { sendMessageToServer, getMessagesFromServer } from './api.js';

const form = document.getElementById('chat-form');
const messageList = document.getElementById('chat-messages');

async function sendMessage(user, text) {
    await sendMessageToServer(user, text);
}

async function loadMessages() {
    const messages = await getMessagesFromServer();
    updateMessageList(messageList, messages);
}

setupChatForm(form, messageList, sendMessage, loadMessages);

setInterval(loadMessages, 2000);
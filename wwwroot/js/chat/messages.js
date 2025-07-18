export function updateMessageList(messageList, messages) {
    messageList.innerHTML = '';
    messages.forEach(msg => {
        const li = document.createElement('li');
        li.innerHTML = `<strong>${msg.userName? msg.userName : "Error"}</strong> (${new Date(msg.timestamp).toLocaleTimeString()}):<br/>${msg.text}`;
        messageList.appendChild(li);
    });
}
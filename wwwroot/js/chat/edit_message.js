import { sendEditMessageToServer } from './api.js';

export function addEditToContainer(chatContainer) {
    chatContainer.addEventListener('click', async (event) => {
        if (event.target.classList.contains('message-options')) {
            const chatMessage = event.target.closest('.chat-message');
            const messageContent = chatMessage.querySelector('.message-content');
            const messageId = messageContent.dataset.id;
            const messageParagraph = messageContent.querySelector('p');
            const currentText = messageParagraph.textContent;

            const { newText, comment, isDelete } = await showEditModal(currentText);

            if (!isDelete && (!newText || newText.trim() === currentText)) {
                return;
            }

            try {
                const result = await sendEditMessageToServer(messageId, newText, comment, isDelete);

                if (result.success) {
                    console.log("Edited");
                } else {
                    alert('Error: ' + (result.error || 'Couldnt edit message'));
                }
            } catch (error) {
                console.error('Error editing:', error);
                alert('Error during editing.');
            }
        }
    });
}

function showEditModal(currentText) {
    return new Promise((resolve) => {
        const overlay = document.createElement('div');
        overlay.classList.add('modal-overlay');

        const modal = document.createElement('div');
        modal.classList.add('modal-window');

        const title = document.createElement('h3');
        title.textContent = 'Edit Message';
        title.classList.add('modal-title');

        const textArea = document.createElement('textarea');
        textArea.value = currentText;
        textArea.rows = 4;
        textArea.placeholder = 'Enter new message text';
        textArea.classList.add('modal-textarea');

        const commentInput = document.createElement('input');
        commentInput.type = 'text';
        commentInput.placeholder = 'Edit comment (optional)';
        commentInput.classList.add('modal-input');

        const buttonContainer = document.createElement('div');
        buttonContainer.classList.add('modal-buttons');

        const saveBtn = document.createElement('button');
        saveBtn.textContent = 'Save';
        saveBtn.classList.add('modal-btn', 'modal-btn-save');

        const cancelBtn = document.createElement('button');
        cancelBtn.textContent = 'Cancel';
        cancelBtn.classList.add('modal-btn', 'modal-btn-cancel');

        const deleteBtn = document.createElement('button');
        deleteBtn.textContent = 'Delete';
        deleteBtn.classList.add('modal-btn', 'modal-btn-delete');

        buttonContainer.appendChild(saveBtn);
        buttonContainer.appendChild(cancelBtn);
        buttonContainer.appendChild(deleteBtn);

        modal.appendChild(title);
        modal.appendChild(textArea);
        modal.appendChild(commentInput);
        modal.appendChild(buttonContainer);
        overlay.appendChild(modal);
        document.body.appendChild(overlay);

        saveBtn.addEventListener('click', () => {
            const newText = textArea.value.trim();
            const comment = commentInput.value.trim();
            document.body.removeChild(overlay);
            resolve({ newText, comment, isDelete: false });
        });

        deleteBtn.addEventListener('click', () => {
            document.body.removeChild(overlay);
            resolve({ newText: null, comment: null, isDelete: true });
        });

        cancelBtn.addEventListener('click', () => {
            document.body.removeChild(overlay);
            resolve({ newText: null, comment: null, isDelete: false });
        });

        overlay.addEventListener('click', (e) => {
            if (e.target === overlay) {
                document.body.removeChild(overlay);
                resolve({ newText: null, comment: null, isDelete: false });
            }
        });
    });
}
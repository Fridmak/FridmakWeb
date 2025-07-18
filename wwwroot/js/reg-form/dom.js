function showError(input, message) {
    const errorElement = document.createElement("div");
    errorElement.className = "text-danger";
    errorElement.textContent = message;

    const parent = input.parentElement;
    const existingError = parent.querySelector(".text-danger");
    if (existingError) {
        parent.removeChild(existingError);
    }
    parent.appendChild(errorElement);

    input.classList.add("is-invalid");
}

function clearError(input) {
    const parent = input.parentElement;
    const errorElement = parent.querySelector(".text-danger");
    if (errorElement) {
        parent.removeChild(errorElement);
    }
    input.classList.remove("is-invalid");
}

export { showError, clearError };
import { isValidEmail, isNonEmpty, doPasswordsMatch } from "./validation.js";
import { showError, clearError } from "./dom.js";

function validateRegistrationForm(form) {
    const usernameInput = form.querySelector('[name="Username"]');
    const emailInput = form.querySelector('[name="Email"]');
    const passwordInput = form.querySelector('[name="Password"]');
    const confirmPasswordInput = form.querySelector('[name="ConfirmPassword"]');

    let isValid = true;

    if (!isNonEmpty(usernameInput.value)) {
        showError(usernameInput, "User name should not be empty");
        isValid = false;
    } else {
        clearError(usernameInput);
    }

    if (!isValidEmail(emailInput.value)) {
        showError(emailInput, "Enter correct email");
        isValid = false;
    } else {
        clearError(emailInput);
    }

    if (!isNonEmpty(passwordInput.value)) {
        showError(passwordInput, "Password could not be null");
        isValid = false;
    } else {
        clearError(passwordInput);
    }

    if (!doPasswordsMatch(passwordInput.value, confirmPasswordInput.value)) {
        showError(confirmPasswordInput, "Passwords doesn't match");
        isValid = false;
    } else {
        clearError(confirmPasswordInput);
    }

    return isValid;
}

window.validateRegistrationForm = validateRegistrationForm;
function isValidEmail(email) {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email.trim());
}

function isNonEmpty(value) {
    return value !== null && value.trim() !== "";
}

function doPasswordsMatch(password, confirmPassword) {
    return password.trim() === confirmPassword.trim();
}

export { isValidEmail, isNonEmpty, doPasswordsMatch };
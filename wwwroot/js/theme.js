(function () {
    const root = document.documentElement;
    const button = document.getElementById('theme-toggle');
    const icon = document.getElementById('theme-icon');

    function applyTheme(theme) {
        root.setAttribute('data-theme', theme);
        localStorage.setItem('theme', theme);

        if (icon) {
            icon.textContent = theme === 'dark' ? '☀️' : '🌙';
        }

        if (button) {
            button.setAttribute(
                'aria-label',
                theme === 'dark' ? 'Включить светлую тему' : 'Включить тёмную тему');
            button.setAttribute(
                'title',
                theme === 'dark' ? 'Светлая тема' : 'Тёмная тема');
        }
    }

    applyTheme(root.getAttribute('data-theme') === 'dark' ? 'dark' : 'light');

    button?.addEventListener('click', function () {
        applyTheme(root.getAttribute('data-theme') === 'dark' ? 'light' : 'dark');
    });
})();

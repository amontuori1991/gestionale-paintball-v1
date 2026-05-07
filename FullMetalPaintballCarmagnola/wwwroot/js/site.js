// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(function () {
    function fireDialog(options) {
        if (window.Swal && typeof window.Swal.fire === 'function') {
            return window.Swal.fire(Object.assign({
                customClass: {
                    popup: 'fmp-swal-popup',
                    confirmButton: 'fmp-swal-confirm',
                    cancelButton: 'fmp-swal-cancel',
                    denyButton: 'fmp-swal-deny'
                },
                buttonsStyling: false
            }, options));
        }

        if (options.showCancelButton) {
            return Promise.resolve({ isConfirmed: window.confirm(options.text || options.title || 'Confermi?') });
        }

        window.alert(options.text || options.title || '');
        return Promise.resolve({ isConfirmed: true });
    }

    window.fmpNotify = function (message, type, title) {
        const icon = type || 'info';
        const defaultTitle = icon === 'success'
            ? 'Operazione completata'
            : icon === 'error'
                ? 'Qualcosa non va'
                : icon === 'warning'
                    ? 'Attenzione'
                    : 'Informazione';

        return fireDialog({
            icon: icon,
            title: title || defaultTitle,
            text: message || '',
            confirmButtonText: 'Ok'
        });
    };

    window.fmpConfirm = function (message, options) {
        options = options || {};
        return fireDialog({
            icon: options.icon || 'question',
            title: options.title || 'Confermi?',
            text: message || '',
            showCancelButton: true,
            confirmButtonText: options.confirmButtonText || 'Conferma',
            cancelButtonText: options.cancelButtonText || 'Annulla'
        }).then(function (result) {
            return !!result.isConfirmed;
        });
    };

    window.fmpPrompt = function (message, options) {
        options = options || {};

        if (window.Swal && typeof window.Swal.fire === 'function') {
            return fireDialog({
                icon: options.icon || 'question',
                title: options.title || 'Inserisci un valore',
                text: message || '',
                input: options.input || 'text',
                inputPlaceholder: options.placeholder || '',
                inputValue: options.value || '',
                showCancelButton: true,
                confirmButtonText: options.confirmButtonText || 'Conferma',
                cancelButtonText: options.cancelButtonText || 'Annulla',
                inputValidator: options.required
                    ? function (value) {
                        return value ? null : (options.requiredMessage || 'Campo obbligatorio.');
                    }
                    : undefined
            }).then(function (result) {
                return result.isConfirmed ? result.value : null;
            });
        }

        return Promise.resolve(window.prompt(message || '', options.value || ''));
    };

    function setLoadingVisible(isVisible, message) {
        const overlay = document.getElementById('fmp-loading-overlay');
        const messageEl = document.getElementById('fmp-loading-message');

        if (!overlay) return;

        if (messageEl && message) {
            messageEl.textContent = message;
        }

        overlay.classList.toggle('is-visible', isVisible);
        overlay.setAttribute('aria-hidden', isVisible ? 'false' : 'true');
    }

    window.fmpShowLoading = function (message) {
        setLoadingVisible(true, message || 'Operazione in corso...');
    };

    window.fmpHideLoading = function () {
        setLoadingVisible(false);
    };

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('form[data-confirm-message]').forEach(function (form) {
            form.addEventListener('submit', function (event) {
                if (form.dataset.confirmed === 'true') {
                    form.dataset.confirmed = 'false';
                    return;
                }

                event.preventDefault();
                fmpConfirm(form.dataset.confirmMessage, {
                    title: form.dataset.confirmTitle || 'Confermi?',
                    icon: form.dataset.confirmIcon || 'warning',
                    confirmButtonText: form.dataset.confirmButton || 'Conferma'
                }).then(function (confirmed) {
                    if (!confirmed) return;
                    form.dataset.confirmed = 'true';
                    form.requestSubmit();
                });
            });
        });

        document.querySelectorAll('form[data-loading-message]').forEach(function (form) {
            form.addEventListener('submit', function (event) {
                if (event.defaultPrevented) {
                    return;
                }

                if (form.dataset.submitting === 'true') {
                    event.preventDefault();
                    return;
                }

                if (typeof form.checkValidity === 'function' && !form.checkValidity()) {
                    return;
                }

                if (window.jQuery && window.jQuery.fn && typeof window.jQuery.fn.valid === 'function') {
                    const $form = window.jQuery(form);
                    if ($form.data('validator') && !$form.valid()) {
                        return;
                    }
                }

                form.dataset.submitting = 'true';
                setLoadingVisible(true, form.dataset.loadingMessage);

                form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(function (button) {
                    button.disabled = true;
                    if (button.tagName === 'BUTTON' && !button.dataset.originalText) {
                        button.dataset.originalText = button.innerHTML;
                        button.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Salvataggio...';
                    }
                });
            });
        });
    });
})();

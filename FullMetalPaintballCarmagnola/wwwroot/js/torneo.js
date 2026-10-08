(() => {
    'use strict';
    const status = document.querySelector('[data-torneo-status]');
    let statusTimer;
    const notify = (message) => {
        if (!status) return;
        window.clearTimeout(statusTimer);
        status.textContent = message;
        statusTimer = window.setTimeout(() => { status.textContent = ''; }, 7000);
    };

    const settings = document.querySelector('[data-torneo-settings]');
    if (settings) {
        // The server binds form decimals using it-IT; accept either keyboard separator.
        settings.addEventListener('submit', () => {
            settings.querySelectorAll('[data-torneo-decimal]').forEach((input) => {
                input.value = input.value.replace('.', ',');
            });
        }, true);
        const toggle = settings.querySelector('[data-finals-toggle]');
        const options = settings.querySelector('[data-finals-options]');
        const update = () => {
            options.hidden = !toggle.checked;
            if (!toggle.checked && !toggle.matches(':disabled')) {
                const third = options.querySelector('[name="FinaleTerzoPosto"]');
                if (third) third.checked = false;
            }
        };
        toggle.addEventListener('change', update);
        update();
    }

    const confirmForms = document.querySelectorAll('form[data-confirm]');
    if (confirmForms.length) {
        const dialog = document.createElement('dialog');
        dialog.className = 'torneo-confirm';
        dialog.setAttribute('aria-labelledby', 'torneo-confirm-title');
        dialog.setAttribute('aria-describedby', 'torneo-confirm-description');
        const title = document.createElement('h2');
        title.id = 'torneo-confirm-title';
        title.textContent = 'Conferma operazione';
        const description = document.createElement('p');
        description.id = 'torneo-confirm-description';
        const actions = document.createElement('div');
        actions.className = 'torneo-actions';
        const cancel = document.createElement('button');
        cancel.type = 'button';
        cancel.className = 'btn btn-outline-secondary';
        cancel.textContent = 'Annulla';
        cancel.autofocus = true;
        const confirm = document.createElement('button');
        confirm.type = 'button';
        confirm.className = 'btn btn-dark';
        confirm.textContent = 'Conferma';
        actions.append(cancel, confirm);
        dialog.append(title, description, actions);
        document.body.append(dialog);
        cancel.addEventListener('click', () => dialog.close('cancel'));
        confirm.addEventListener('click', () => dialog.close('confirm'));
        let pendingForm;
        let pendingSubmitter;
        const approved = new WeakSet();
        dialog.addEventListener('close', () => {
            const form = pendingForm;
            const submitter = pendingSubmitter;
            pendingForm = null;
            pendingSubmitter = null;
            if (dialog.returnValue === 'confirm' && form) {
                approved.add(form);
                if (submitter) form.requestSubmit(submitter);
                else form.requestSubmit();
                approved.delete(form);
            }
        });
        confirmForms.forEach((form) => {
            form.addEventListener('submit', (event) => {
                if (approved.has(form)) return;
                event.preventDefault();
                if (dialog.open) return;
                pendingForm = form;
                pendingSubmitter = event.submitter;
                description.textContent = form.dataset.confirm;
                dialog.returnValue = '';
                dialog.showModal();
            });
        });
    }

    document.querySelectorAll('[data-copy-target]').forEach((button) => {
        button.addEventListener('click', async () => {
            const input = document.getElementById(button.dataset.copyTarget);
            try {
                await navigator.clipboard.writeText(input.value);
                notify('Link copiato. Condividilo solo con i partecipanti.');
            } catch {
                input.focus();
                input.select();
                notify('Seleziona e copia il link con il menu del dispositivo.');
            }
        });
    });

    const whatsappNumber = (raw) => {
        const trimmed = (raw || '').trim();
        if (!trimmed) return '';
        const digits = trimmed.replace(/\D/g, '');
        if (trimmed.startsWith('+')) return digits;
        if (trimmed.startsWith('00')) return digits.slice(2);
        if (digits.length === 10) return '39' + digits;
        return digits;
    };
    document.querySelectorAll('[data-wa-number]').forEach((button) => {
        button.addEventListener('click', async () => {
            const number = whatsappNumber(button.dataset.waNumber);
            if (!/^\d{7,15}$/.test(number)) {
                notify('Aggiungi un telefono valido con prefisso internazionale nella scheda della squadra.');
                return;
            }
            let popup;
            let pendingUrl;
            try {
                let message = button.dataset.waMessage;
                if (button.dataset.waEndpoint) {
                    // Open synchronously so the browser does not block the asynchronous share.
                    popup = window.open('about:blank', '_blank');
                    if (popup) popup.opener = null;
                    button.disabled = true;
                    const response = await fetch(button.dataset.waEndpoint, { headers: { Accept: 'application/json' } });
                    if (!response.ok || response.redirected) throw new Error('request');
                    const data = await response.json();
                    if (!data.message || typeof data.message !== 'string') throw new Error('message');
                    message = data.message;
                }
                pendingUrl = 'https://wa.me/' + number + '?text=' + encodeURIComponent(message || '');
                if (popup && !popup.closed) popup.location.replace(pendingUrl);
                else window.location.assign(pendingUrl);
            } catch {
                if (popup && !popup.closed) popup.close();
                notify('Messaggio non disponibile. Ricarica la pagina o riprova tra poco.');
            } finally {
                button.disabled = false;
            }
        });
    });

    const pending = document.querySelector('[data-pending-only]');
    if (pending) {
        pending.addEventListener('change', () => {
            let visible = 0;
            document.querySelectorAll('[data-match-group]').forEach((group) => {
                let groupVisible = 0;
                group.querySelectorAll('[data-played]').forEach((match) => {
                    match.hidden = pending.checked && match.dataset.played === 'true';
                    if (!match.hidden) groupVisible++;
                });
                group.hidden = groupVisible === 0;
                visible += groupVisible;
            });
            document.querySelector('[data-no-pending]').hidden = !pending.checked || visible > 0;
        });
    }
})();

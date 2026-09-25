(() => {
    'use strict';
    const feedback = document.getElementById('album-feedback');
    const form = document.getElementById('album-upload');
    let busy = false;
    const tell = message => { if (feedback) feedback.textContent = message; };
    document.querySelectorAll('.album-expiry').forEach(time => {
        time.textContent = new Date(time.dateTime).toLocaleString(document.documentElement.lang || 'it', { dateStyle: 'short', timeStyle: 'short' });
    });
    document.getElementById('album-copy')?.addEventListener('click', async () => {
        const input = document.getElementById('album-url');
        try { await navigator.clipboard.writeText(input.value); tell('Link copiato.'); }
        catch { input.focus(); input.select(); tell('Seleziona Copia per copiare il link evidenziato.'); }
    });
    const errorMessage = async response => {
        if (response.status === 413) return 'Foto troppo grande: massimo 15 MB.';
        if (response.status === 401 || response.status === 403 || response.redirected) return 'Sessione scaduta o accesso non consentito. Ricarica la pagina e accedi di nuovo.';
        try { return (await response.json()).message || 'Operazione non riuscita. Riprova.'; }
        catch { return 'Operazione non riuscita. Ricarica la pagina prima di riprovare.'; }
    };
    const upload = (url, data, onProgress) => new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        xhr.open('POST', url);
        xhr.timeout = 180000;
        xhr.upload.onprogress = event => {
            if (event.lengthComputable) onProgress(Math.floor(event.loaded / event.total * 100));
        };
        xhr.upload.onload = () => onProgress(100);
        xhr.onload = () => {
            let body;
            try { body = JSON.parse(xhr.responseText); } catch { /* A login page is not a successful upload. */ }
            if (xhr.status >= 200 && xhr.status < 300 && body?.message && xhr.responseURL === new URL(url, location.href).href) resolve();
            else reject(new Error(xhr.status === 413 ? 'Foto troppo grande: massimo 15 MB.' : body?.message || 'Operazione non riuscita o sessione scaduta. Verifica l\'album prima di riprovare.'));
        };
        xhr.onerror = xhr.ontimeout = xhr.onabort = () => reject(new Error('Connessione interrotta o tempo scaduto. Verifica l\'album prima di riprovare.'));
        xhr.send(data);
    });
    form?.addEventListener('submit', async event => {
        event.preventDefault();
        if (busy || !form.reportValidity()) return;
        const files = [...document.getElementById('album-files').files];
        if (!files.length) return;
        const status = document.getElementById('album-status');
        const failures = document.getElementById('album-failures');
        const progress = document.getElementById('album-progress');
        const token = form.querySelector('[name="__RequestVerificationToken"]').value;
        const id = form.querySelector('[name="id"]').value;
        failures.replaceChildren();
        busy = true;
        document.querySelectorAll('.album-delete').forEach(b => b.disabled = true);
        [...form.elements].forEach(input => input.disabled = true);
        progress.hidden = false;
        let done = 0;
        let failed = 0;
        for (const file of files) {
            const label = `Foto ${done + failed + 1} di ${files.length}`;
            progress.value = 0;
            status.textContent = `${label}: caricamento 0%.`;
            try {
                if (file.size > 15 * 1024 * 1024) throw new Error('Massimo 15 MB per foto.');
                if (file.type.startsWith('video/') || !/\.(jpe?g|png|webp|bmp|gif)$/i.test(file.name))
                    throw new Error('Solo immagini JPG, PNG, WebP, BMP e GIF statiche. Video esclusi; converti gli altri formati in JPG.');
                const data = new FormData();
                data.append('__RequestVerificationToken', token);
                data.append('id', id);
                data.append('autorizzato', 'true');
                data.append('foto', file);
                await upload(form.action, data, percent => {
                    status.textContent = percent < 100
                        ? `${label}: caricamento ${percent}%.`
                        : `${label}: trasferimento 100%. Applicazione logo e salvataggio in corso, attendi...`;
                    if (percent < 100) progress.value = percent;
                    else progress.removeAttribute('value');
                });
                done++;
            } catch (error) {
                failed++;
                const li = document.createElement('li');
                li.textContent = `${file.name}: ${error.message}`;
                failures.appendChild(li);
            }
            progress.value = 100;
        }
        busy = false;
        [...form.elements].forEach(input => input.disabled = false);
        document.querySelectorAll('.album-delete').forEach(b => b.disabled = false);
        document.getElementById('album-files').value = '';
        status.textContent = `${done} foto caricate. ${failed} non caricate.`;
        if (failed === 0) { window.location.reload(); return; }
        const reload = document.createElement('button');
        reload.type = 'button'; reload.className = 'album-button'; reload.textContent = 'Aggiorna album';
        reload.addEventListener('click', () => window.location.reload());
        status.append(' ', reload);
    });
    document.querySelectorAll('.album-delete').forEach(button => button.addEventListener('click', async () => {
        if (busy || button.disabled) return;
        const confirmed = window.fmpConfirm
            ? await window.fmpConfirm('La foto non sar\u00e0 pi\u00f9 scaricabile dal cliente.', { title: 'Eliminare questa foto?', icon: 'warning', confirmButtonText: 'Elimina' })
            : window.confirm('Eliminare definitivamente questa foto?');
        if (!confirmed) return;
        button.disabled = true;
        const data = new FormData();
        data.append('__RequestVerificationToken', document.querySelector('[name="__RequestVerificationToken"]').value);
        data.append('id', button.dataset.game); data.append('photo', button.dataset.photo);
        try {
            const response = await fetch(button.dataset.url, { method: 'POST', body: data, credentials: 'same-origin' });
            if (!response.ok || response.redirected) throw new Error(await errorMessage(response));
            window.location.reload();
        } catch (error) { tell(error.message); button.disabled = false; }
    }));
    window.addEventListener('beforeunload', event => { if (busy) { event.preventDefault(); event.returnValue = ''; } });
})();

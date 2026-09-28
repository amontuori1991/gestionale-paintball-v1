(() => {
    'use strict';
    const form = document.getElementById('flyer-form');
    if (!form) return;
    const status = document.getElementById('flyer-status');
    const preview = document.getElementById('flyer-preview');
    const buttons = [...document.querySelectorAll('[data-export]')];
    let timer, request, revision = 0, previewUrl, exporting = false;
    async function generate(format) {
        if (exporting || !form.reportValidity()) return;
        clearTimeout(timer);
        request?.abort();
        request = new AbortController();
        const mine = ++revision;
        exporting = format !== 'preview';
        buttons.forEach(b => b.disabled = true);
        status.textContent = exporting ? 'Preparazione del file...' : 'Aggiornamento anteprima...';
        try {
            const data = new FormData(form);
            data.set('Format', format);
            const response = await fetch(form.action, { method: 'POST', body: data, signal: request.signal });
            if (!response.ok || response.redirected) {
                let message = 'Generazione non riuscita. Verifica la sessione e riprova.';
                try { message = (await response.json()).message || message; } catch { }
                throw new Error(message);
            }
            const expected = format === 'pdf' ? 'application/pdf' : 'image/jpeg';
            if (!response.headers.get('content-type')?.startsWith(expected)) throw new Error('Risposta non valida. Accedi nuovamente.');
            const blob = await response.blob();
            if (mine !== revision) return;
            const url = URL.createObjectURL(blob);
            if (format === 'preview') {
                if (previewUrl) URL.revokeObjectURL(previewUrl);
                previewUrl = url; preview.src = url; preview.hidden = false;
                document.getElementById('flyer-placeholder').hidden = true;
                status.textContent = 'Anteprima aggiornata.';
            } else {
                const a = document.createElement('a'); a.href = url; a.download = `Volantino-FullMetal.${format}`;
                document.body.appendChild(a); a.click(); a.remove();
                setTimeout(() => URL.revokeObjectURL(url), 60000);
                status.textContent = 'File pronto. Download avviato.';
            }
        } catch (error) {
            if (error.name !== 'AbortError') status.textContent = error.message;
        } finally {
            if (mine === revision) { exporting = false; buttons.forEach(b => b.disabled = false); }
        }
    }
    form.addEventListener('submit', e => { e.preventDefault(); generate('preview'); });
    form.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(() => generate('preview'), 800); });
    buttons.forEach(b => b.addEventListener('click', () => generate(b.dataset.export)));
    const presets = {
        party: ['IL COMPLEANNO CAMBIA CAMPO.', 'Una festa da vivere. Non solo da fotografare.', 'FESTEGGIA CON LA TUA SQUADRA'],
        challenge: ['AMICI FUORI. RIVALI IN CAMPO.', 'Forma la squadra e accetta la sfida.', 'LA PROSSIMA SFIDA TI ASPETTA'],
        event: ['SCOPRI IL PAINTBALL.', 'Una giornata per mettersi in gioco.', 'OPEN DAY / VIENI A CONOSCERCI']
    };
    document.querySelectorAll('[data-preset]').forEach(b => b.addEventListener('click', () => {
        const values = presets[b.dataset.preset];
        ['Title', 'Subtitle', 'Offer'].forEach((name, i) => form.elements[name].value = values[i]);
        generate('preview');
    }));
    generate('preview');
})();

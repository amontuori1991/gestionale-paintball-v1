(() => {
    'use strict';
    const type = document.getElementById('Type');
    const unlimited = document.getElementById('Unlimited');
    type?.addEventListener('change', () => { unlimited.checked = type.value === 'Kids'; });
    document.querySelectorAll('form[data-confirm]').forEach(form => form.addEventListener('submit', event => {
        if (!window.confirm(form.dataset.confirm)) event.preventDefault();
    }));
    const share = document.querySelector('[data-share-url]');
    share?.addEventListener('click', async () => {
        const status = document.getElementById('share-status');
        share.disabled = true;
        try {
            const response = await fetch(share.dataset.shareUrl);
            if (!response.ok || !response.headers.get('content-type')?.startsWith('image/')) throw new Error('Download non disponibile. Ricarica la pagina.');
            const file = new File([await response.blob()], share.dataset.shareName, {type:'image/jpeg'});
            if (navigator.canShare?.({files:[file]})) await navigator.share({files:[file], title:'Il tuo buono regalo Full Metal'});
            else status.textContent = 'Scarica il JPG con il pulsante dedicato e allegalo nella chat WhatsApp.';
        } catch (error) { if(error.name !== 'AbortError') status.textContent = error.message; }
        finally { share.disabled = false; }
    });
    const start = document.getElementById('scan-start');
    if (!start) return;
    const stop = document.getElementById('scan-stop');
    const status = document.getElementById('scan-status');
    const fileInput = document.getElementById('scan-file');
    let scanner, running = false, busy = false, decoded = false;
    async function close() {
        if (running) { await scanner.stop(); running = false; }
        stop.hidden = true;
    }
    async function found(value) {
        if(decoded) return;
        const code = value.trim().toUpperCase();
        if (!/^FMP-[A-F0-9]{20}$/.test(code)) { status.textContent = 'Questo codice non e un buono regalo Full Metal.'; return; }
        decoded = true;
        await close();
        document.getElementById('voucher-query').value = code;
        document.querySelector('[name=stato]').value = '';
        document.getElementById('voucher-search').requestSubmit();
    }
    function reader() {
        if (!window.Html5Qrcode) throw new Error('Lettore non disponibile: inserisci il codice manualmente.');
        return scanner ||= new Html5Qrcode('voucher-reader');
    }
    start.addEventListener('click', async () => {
        if(busy || running) return;
        busy=true; start.disabled=true; fileInput.disabled=true; decoded=false;
        try {
            await reader().start({facingMode:'environment'}, {fps:8, qrbox:{width:230,height:230}}, value => { if(running) found(value).catch(() => { status.textContent='Errore di lettura. Inserisci il codice manualmente.'; }); }, () => {});
            running=true; stop.hidden=false; status.textContent='Inquadra il QR del buono.';
        } catch { status.textContent='Fotocamera non disponibile o permesso negato. Usa una foto del QR oppure inserisci il codice.'; }
        finally { busy=false; start.disabled=false; fileInput.disabled=false; }
    });
    stop.addEventListener('click', () => close().catch(() => { status.textContent='Chiudi questa pagina per interrompere la fotocamera.'; }));
    fileInput.addEventListener('change', async () => {
        const file = fileInput.files?.[0]; if(!file || busy) return;
        busy=true; start.disabled=true; decoded=false;
        try { await close(); const value=await reader().scanFile(file,true); await found(value); }
        catch { status.textContent='QR non leggibile. Prova una foto piu nitida o inserisci il codice manualmente.'; }
        finally { busy=false; start.disabled=false; fileInput.value=''; }
    });
    window.addEventListener('pagehide', () => { if(running) scanner.stop().catch(() => {}); });
})();

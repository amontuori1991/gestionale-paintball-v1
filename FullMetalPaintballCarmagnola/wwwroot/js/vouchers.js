(() => {
    'use strict';
    const type = document.getElementById('Type');
    const unlimited = document.getElementById('Unlimited');
    const mode = document.getElementById('Mode');
    const priceData = document.getElementById('voucher-price-data');
    if (mode && priceData) {
        const data = JSON.parse(priceData.textContent);
        const amount = document.getElementById('Amount');
        const duration = document.getElementById('Duration');
        const people = document.getElementById('People');
        const rabbit = document.getElementById('Rabbit');
        const info = document.getElementById('voucher-price-info');
        const money = value => Number(value).toLocaleString('it-IT', {minimumFractionDigits:2,maximumFractionDigits:2,useGrouping:false});
        let customAmount = mode.value === 'amount' ? amount.value : '';
        function sync() {
            const isPackage = mode.value === 'package';
            const fields = document.getElementById('voucher-package');
            fields.hidden = !isPackage; fields.disabled = !isPackage;
            document.getElementById('voucher-money-note').hidden = isPackage;
            document.getElementById('voucher-show-amount').hidden = !isPackage;
            amount.readOnly = isPackage; amount.setCustomValidity('');
            if (!isPackage) { info.textContent = 'Il valore viene sempre riportato sul buono economico.'; return; }
            if (type.value === 'Kids') unlimited.checked = true;
            const restricted = type.value === 'Adulti' && duration.value === '2';
            if (restricted) unlimited.checked = false;
            unlimited.disabled = restricted || type.value === 'Kids';
            const shots = type.value === 'Kids' || unlimited.checked ? 'unlimited' : 'standard';
            const legacy = data.legacy;
            if (legacy && legacy.Type === type.value && legacy.Duration === duration.value &&
                legacy.People === Number(people.value) && (legacy.Rabbit || 0) === Number(rabbit.value) &&
                (legacy.Type === 'Kids' || legacy.Unlimited) === (shots === 'unlimited')) {
                amount.value = money(legacy.Amount);
                info.textContent = 'Buono precedente: importo originale conservato. Cambiando pacchetto si applica il listino attuale.';
                return;
            }
            const unit = data.prices[`${type.value}|${shots}|${duration.value}`];
            const extra = rabbit.value === '0' ? 0 : data.prices['rabbit' + rabbit.value];
            if (unit == null || unit <= 0 || extra == null || Number(people.value) < 1 || Number(people.value) > 100) {
                amount.value = ''; info.textContent = 'Seleziona un pacchetto disponibile e un numero valido di partecipanti.';
                amount.setCustomValidity('Pacchetto non disponibile.'); return;
            }
            amount.value = money(unit * Number(people.value) + extra);
            info.textContent = `${data.name}${data.frozen ? ' (prezzi conservati all\'emissione)' : ' (in vigore)'}: ${money(unit)} euro x ${people.value} persone${extra ? ' + ' + money(extra) + ' euro Caccia al coniglio' : ''} = ${amount.value} euro.`;
        }
        type.addEventListener('change', () => { unlimited.checked = type.value === 'Kids'; sync(); });
        [duration, people, rabbit, unlimited].forEach(input => input.addEventListener('input', sync));
        amount.addEventListener('input', () => { if(mode.value === 'amount') customAmount = amount.value; });
        mode.addEventListener('change', () => { if(mode.value === 'amount') amount.value = customAmount; sync(); });
        sync();
    }
    const confirmation = document.createElement('dialog');
    confirmation.className = 'voucher-confirm-dialog';
    confirmation.setAttribute('aria-labelledby', 'voucher-confirm-title');
    confirmation.setAttribute('aria-describedby', 'voucher-confirm-text');
    confirmation.innerHTML = '<form method="dialog"><span class="voucher-confirm-eyebrow">BUONI REGALO</span><h2 id="voucher-confirm-title"></h2><p id="voucher-confirm-text"></p><div class="voucher-actions"><button value="cancel" class="btn btn-outline-dark" autofocus>Torna indietro</button><button value="confirm" class="btn btn-dark" id="voucher-confirm-yes"></button></div></form>';
    document.body.appendChild(confirmation);
    window.addEventListener('pageshow', () => {
        document.querySelectorAll('form[data-confirm]').forEach(form => {
            delete form.dataset.submitted;
            delete form.dataset.confirmed;
        });
    });
    document.querySelectorAll('form[data-confirm]').forEach(form => form.addEventListener('submit', async event => {
        if (form.dataset.confirmed === 'true') { form.dataset.confirmed = ''; return; }
        event.preventDefault();
        if (confirmation.open || form.dataset.submitted === 'true') return;
        const submitter = event.submitter;
        const titles = {pagato:'Conferma pagamento',riscatta:'Riscatta il buono',ripristina:'Annulla il riscatto',annulla:'Annulla il buono'};
        confirmation.querySelector('h2').textContent = form.dataset.confirmTitle || titles[submitter?.value] || 'Conferma operazione';
        confirmation.querySelector('p').textContent = form.dataset.confirm;
        const yes = confirmation.querySelector('#voucher-confirm-yes');
        yes.textContent = submitter?.textContent.trim() || 'Conferma';
        yes.className = form.dataset.confirmDanger === 'true' || submitter?.value === 'annulla' ? 'btn btn-danger' : 'btn btn-dark';
        confirmation.returnValue = 'cancel';
        const result = new Promise(resolve => confirmation.addEventListener('close', () => resolve(confirmation.returnValue === 'confirm'), {once:true}));
        confirmation.showModal();
        if (await result) {
            form.dataset.confirmed = 'true';
            form.dataset.submitted = 'true';
            if (submitter) form.requestSubmit(submitter);
            else form.requestSubmit();
        }
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

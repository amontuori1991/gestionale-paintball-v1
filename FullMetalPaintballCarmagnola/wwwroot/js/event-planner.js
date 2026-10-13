(() => {
    const root = document.getElementById('event-planner');
    if (!root) return;
    const form = document.getElementById('event-planner-form');
    const status = document.getElementById('event-planner-status');
    const results = document.getElementById('event-planner-results');
    const more = document.getElementById('event-planner-more');
    let proposals = [], shown = 0;
    let request, timer, version = 0;
    function element(tag, text) {
        const node = document.createElement(tag);
        node.textContent = text;
        return node;
    }
    async function calculate() {
        const current = ++version;
        request?.abort();
        results.replaceChildren();
        more.hidden = true;
        if (!form.checkValidity()) { status.textContent = 'Completa le date per calcolare le proposte.'; return; }
        request = new AbortController();
        status.textContent = 'Confronto disponibilita e storico in corso...';
        try {
            const response = await fetch(`${root.dataset.url}?${new URLSearchParams(new FormData(form))}`, {signal: request.signal});
            if (response.redirected || !response.headers.get('content-type')?.includes('application/json')) throw new Error('Sessione scaduta: aggiorna la pagina e accedi nuovamente.');
            const data = await response.json();
            if (!response.ok) throw new Error(data.errore || 'Calcolo non disponibile. Riprova.');
            if (current !== version) return;
            status.textContent = `${data.proposte.length} giornate disponibili. Mostriamo prima le fasce meno occupate nello storico.`;
            if (data.giorniEsclusi) status.textContent += ` ${data.giorniEsclusi} giornate escluse per eventi senza orario completo.`;
            if (!data.proposte.length) status.textContent += ' Prova una durata inferiore o un periodo diverso.';
            proposals = data.proposte;
            shown = 0;
            renderMore();
        } catch (error) {
            if (error.name !== 'AbortError' && current === version) status.textContent = error.message;
        }
    }
    function renderMore() {
            for (const p of proposals.slice(shown, shown + 3)) {
                const card = element('article', ''); card.className = 'event-proposal';
                const day = new Date(`${p.data.slice(0,10)}T12:00:00`).toLocaleDateString('it-IT', {weekday:'long',day:'2-digit',month:'long',year:'numeric'});
                card.append(element('h3', day), element('strong', `${p.inizio} - ${p.fine}`));
                card.append(element('p', 'Questa fascia risulta libera al momento del calcolo.'));
                card.append(element('p', p.occupazione == null ? 'Dati insufficienti per consigliare questa data.' :
                    `${p.dateConfronto[0]}: ${p.partite === 0 ? 'nessuna partita confermata' : p.partite === 1 ? '1 partita confermata' : `${p.partite} partite confermate`} nella stessa fascia.`));
                card.append(element('small', `Fine evento entro la chiusura delle ${p.chiusura}. Tramonto ${p.tramonto}.`));
                results.append(card);
            }
            shown += 3;
            more.hidden = shown >= proposals.length;
    }
    more.addEventListener('click', renderMore);
    form.addEventListener('submit', event => { event.preventDefault(); clearTimeout(timer); calculate(); });
    form.addEventListener('input', () => { ++version; request?.abort(); results.replaceChildren(); more.hidden = true; status.textContent = 'Parametri modificati, ricalcolo...'; clearTimeout(timer); timer = setTimeout(calculate, 350); });
    calculate();
})();

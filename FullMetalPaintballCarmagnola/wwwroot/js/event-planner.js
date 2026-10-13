(() => {
    const root = document.getElementById('event-planner');
    if (!root) return;
    const form = document.getElementById('event-planner-form');
    const status = document.getElementById('event-planner-status');
    const results = document.getElementById('event-planner-results');
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
        if (!form.checkValidity()) { status.textContent = 'Completa le date per calcolare le proposte.'; return; }
        request = new AbortController();
        status.textContent = 'Confronto disponibilita e storico in corso...';
        try {
            const response = await fetch(`${root.dataset.url}?${new URLSearchParams(new FormData(form))}`, {signal: request.signal});
            if (response.redirected || !response.headers.get('content-type')?.includes('application/json')) throw new Error('Sessione scaduta: aggiorna la pagina e accedi nuovamente.');
            const data = await response.json();
            if (!response.ok) throw new Error(data.errore || 'Calcolo non disponibile. Riprova.');
            if (current !== version) return;
            status.textContent = `${data.proposte.length} giornate con una fascia disponibile. ${data.giorniEsclusi} giornate escluse per eventi o amichevoli senza orario completo.`;
            if (!data.proposte.length) status.textContent += ' Prova una durata inferiore o un periodo diverso.';
            for (const p of data.proposte) {
                const card = element('article', ''); card.className = 'event-proposal';
                const day = new Date(`${p.data.slice(0,10)}T12:00:00`).toLocaleDateString('it-IT', {weekday:'long',day:'2-digit',month:'long',year:'numeric'});
                card.append(element('small', 'DISPONIBILE AL MOMENTO DEL CALCOLO'), element('h3', day), element('strong', `${p.inizio} - ${p.fine}`));
                card.append(element('p', p.occupazione == null ? 'Storico non disponibile: non classificabile.' : `${p.occupazione.toLocaleString('it-IT')}% di tempo occupato nel campione ${p.annoStorico}`));
                card.append(element('small', `${p.campioni} giornate confrontabili, ${p.partite} partite nella fascia.${p.campioni < 3 ? ' Campione insufficiente per una raccomandazione attendibile.' : ' Indicazione storica, non previsione garantita.'}`));
                card.append(element('p', `Tramonto ${p.tramonto} · Chiusura ${p.chiusura}`));
                if (p.dateConfronto.length) {
                    const details = element('details', '');
                    details.append(element('summary', 'Date confrontate'), element('p', p.dateConfronto.join(' · ')));
                    card.append(details);
                }
                results.append(card);
            }
        } catch (error) {
            if (error.name !== 'AbortError' && current === version) status.textContent = error.message;
        }
    }
    form.addEventListener('submit', event => { event.preventDefault(); clearTimeout(timer); calculate(); });
    form.addEventListener('input', () => { ++version; request?.abort(); results.replaceChildren(); status.textContent = 'Parametri modificati, ricalcolo...'; clearTimeout(timer); timer = setTimeout(calculate, 350); });
    calculate();
})();

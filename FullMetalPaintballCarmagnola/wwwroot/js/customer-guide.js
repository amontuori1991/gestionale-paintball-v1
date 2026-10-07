(() => {
    const search = document.getElementById('FaqSearch');
    const items = [...document.querySelectorAll('.faq-item')];
    const normalize = text => text.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
    search?.addEventListener('input', () => {
        const words = normalize(search.value.trim()).split(/\s+/).filter(Boolean);
        let count = 0;
        for (const item of items) {
            const matches = words.every(word => normalize(item.textContent).includes(word));
            item.hidden = !matches;
            item.open = words.length > 0 && matches;
            if (matches) count++;
        }
        document.getElementById('FaqSearchStatus').textContent = !words.length ? '' : count
            ? `${count} risposte trovate.`
            : 'Nessuna risposta trovata. Prova con una parola diversa oppure scrivici per questa richiesta particolare.';
    });
})();

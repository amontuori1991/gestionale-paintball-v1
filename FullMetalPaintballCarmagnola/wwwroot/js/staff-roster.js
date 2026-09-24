document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.roster-page').forEach(page => {
        const input = page.querySelector('.roster-search');
        const days = [...page.querySelectorAll('.roster-day')];
        const empty = page.querySelector('.roster-empty');
        const normalize = text => text.toLocaleLowerCase('it').normalize('NFD').replace(/[\u0300-\u036f]/g, '');
        function filterDays() {
            const query = normalize(input.value.trim());
            let visible = 0;
            days.forEach(day => {
                // Ignore unselected options when searching for availability.
                const copy = day.cloneNode(true);
                copy.querySelectorAll('select').forEach(select => select.remove());
                const values = [...day.querySelectorAll('select')].map(select => select.selectedOptions[0]?.textContent || '');
                const labels = [...day.querySelectorAll('[data-label]')].map(field => field.dataset.label);
                const dates = [...day.querySelectorAll('time')].map(time => time.dataset.searchDate + ' ' + time.dateTime);
                day.hidden = !normalize([copy.textContent, ...values, ...labels, ...dates].join(' ')).includes(query);
                if (!day.hidden) visible++;
            });
            empty.hidden = visible > 0;
        }
        input.addEventListener('input', filterDays);
        filterDays();
    });
});

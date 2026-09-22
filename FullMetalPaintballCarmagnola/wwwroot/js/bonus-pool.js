(() => {
    const game = document.getElementById('bonus-game');
    if (!game) return;
    const proposeStaff = () => {
        const option = game.selectedOptions[0];
        if (!option?.dataset.staff) return;
        const names = JSON.parse(option.dataset.staff).map(n => n.trim().toLocaleLowerCase());
        document.querySelectorAll('input[name="Form.Attendees"]').forEach(input => {
            input.checked = names.includes(input.value.toLocaleLowerCase());
        });
    };
    game.addEventListener('change', () => {
        const option = game.selectedOptions[0];
        if (!option?.value) return;
        document.getElementById('Form_Date').value = option.dataset.date;
        document.getElementById('Form_Time').value = option.dataset.time;
        document.getElementById('Form_Minutes').value = option.dataset.minutes;
        proposeStaff();
    });
    if (!document.querySelector('input[name="Form.Attendees"]:checked')) proposeStaff();
})();

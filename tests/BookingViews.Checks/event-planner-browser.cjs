const { chromium } = require('playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        for (const width of [390, 1440]) {
            const page = await browser.newPage({ viewport: { width, height: 900 } });
            await page.route('https://planner.test/**', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({
                giorniEsclusi: 0, proposte: Array.from({length: 7}, (_, i) => ({
                    data: '2026-10-18T00:00:00', inizio: '09:00', fine: '13:00', chiusura: '18:00', tramonto: '18:40',
                    occupazione: i === 6 ? null : 0, partite: 0, dateConfronto: ['19/10/2025']
                }))
            }) }));
            await page.setContent(`<section id="event-planner" data-url="https://planner.test/api" class="event-planner">
                <form id="event-planner-form"><input name="dal" value="2026-10-18"><input name="al" value="2026-11-18"></form>
                <p id="event-planner-status"></p><div id="event-planner-results" class="event-planner-results"></div>
                <button id="event-planner-more" hidden>Mostra altre proposte</button></section>`);
            await page.addStyleTag({content: fs.readFileSync('FullMetalPaintballCarmagnola/wwwroot/css/event-planner.css', 'utf8')});
            await page.addScriptTag({content: fs.readFileSync('FullMetalPaintballCarmagnola/wwwroot/js/event-planner.js', 'utf8')});
            await page.waitForFunction(() => document.querySelectorAll('.event-proposal').length === 3);
            assert(!(await page.locator('#event-planner-results').innerText()).includes('%'));
            assert((await page.locator('#event-planner-results').innerText()).includes('19/10/2025'));
            await page.locator('#event-planner-more').click();
            assert.equal(await page.locator('.event-proposal').count(), 6);
            await page.locator('#event-planner-more').click();
            assert.equal(await page.locator('.event-proposal').count(), 7);
            assert(await page.locator('#event-planner-more').isHidden());
            assert((await page.locator('.event-proposal').last().innerText()).includes('Dati insufficienti'));
            assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
            await page.close();
        }
        console.log('PASS: desktop/mobile, three proposals, show more, missing history, no percentages');
    } finally { await browser.close(); }
})();

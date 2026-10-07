// Uses the disposable GiftVouchers.Checks --preview server; never sends WhatsApp messages.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
(async () => {
    const browser = await chromium.launch({channel:'msedge', headless:true});
    try {
        const page = await browser.newPage();
        const errors = []; page.on('pageerror', error => errors.push(error.message));
        for (const width of [390,1440]) {
            await page.setViewportSize({width,height:900});
            const response = await page.goto('http://127.0.0.1:55447/Prenotazione');
            assert.equal(response.status(),200);
            assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
            await page.locator('#FaqSearch').fill('docce');
            assert.equal(await page.locator('.faq-item:visible').count(),1);
            assert(await page.locator('.faq-item:visible').getAttribute('open') !== null);
            await page.locator('#FaqSearch').fill('zzzzzzzz');
            assert.equal(await page.locator('.faq-item:visible').count(),0);
            await page.locator('#FaqSearch').fill('');
            assert(await page.locator('.faq-item:visible').count()>1);
            await page.screenshot({path:`.codex-build/vouchers/customer-guide-${width}.png`,fullPage:true});
        }
        await page.locator('[data-mode-button=weekend]').click();
        await page.locator('#CustomerName').fill('Cliente Test');
        await page.locator('#Participants').fill('10');
        await page.locator('#DepositMethod').selectOption('Satispay');
        const date=await page.locator('#BookingDate option').nth(1).getAttribute('value');
        await page.locator('#BookingDate').selectOption(date);
        await page.locator('#SlotDays button').first().click();
        await page.evaluate(() => { window.open = url => { window.testWhatsappUrl=url; }; });
        await page.locator('#WhatsappButton').click();
        const url=await page.evaluate(() => window.testWhatsappUrl);
        assert(url && new URL(url).hostname==='wa.me');
        const message=new URL(url).searchParams.get('text');
        assert(message.includes('Metodo caparra: Satispay'));
        assert(message.includes('Cliente Test'));
        assert(message.includes('attesa di conferma'));
        assert.equal(errors.length,0,errors.join('\n'));
        console.log('PASS: anonymous page, responsive layout, FAQ search, complete WhatsApp draft with payment preference. No message sent.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error);process.exit(1); });

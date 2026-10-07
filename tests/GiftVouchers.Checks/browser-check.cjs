// Run against the disposable --preview server, never production.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
(async () => {
    const browser = await chromium.launch({channel:'msedge',headless:true});
    const context = await browser.newContext();
    await context.addCookies([{name:'VoucherTestRole',value:'Admin',url:'http://127.0.0.1:55447'}]);
    const page = await context.newPage();
    const errors=[];page.on('pageerror', e=>errors.push(e.message));
    for (const width of [390,1440]) {
        await page.setViewportSize({width,height:900});
        await page.goto('http://127.0.0.1:55447/BuoniRegalo');
        await page.screenshot({path:`.codex-build/vouchers/index-${width}.png`,fullPage:true});
        assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth), 'Index overflow');
        await page.getByRole('link',{name:'Apri buono'}).first().click();
        await page.locator('.voucher-preview').waitFor();
        await page.screenshot({path:`.codex-build/vouchers/details-${width}.png`,fullPage:true});
        assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth), 'Details overflow');
    }
    await page.goto('http://127.0.0.1:55447/BuoniRegalo');
    await page.locator('#scan-file').setInputFiles('.codex-build/vouchers/voucher.jpg.jpg');
    await page.waitForURL(/q=FMP-/);
    assert.equal(await page.locator('.voucher-item').count(),1,'Image QR search');
    await page.goto('http://127.0.0.1:55447/BuoniRegalo/Crea');
    await page.setViewportSize({width:390,height:900});
    await page.locator('#Recipient').fill('Test browser'); await page.locator('#Buyer').fill('Buyer');
    await page.locator('#Amount').fill('240,50');
    await page.locator('#Type').selectOption('Kids'); assert(await page.locator('#Unlimited').isChecked());
    await page.locator('#Type').selectOption('Adulti'); assert(!await page.locator('#Unlimited').isChecked());
    await page.screenshot({path:'.codex-build/vouchers/create-mobile.png',fullPage:true});
    await page.getByRole('button',{name:'Salva e visualizza anteprima'}).click();
    try { await page.waitForURL(/Dettaglio/,{timeout:10000}); }
    catch(error) { console.error(await page.locator('main').innerText()); console.error(await page.locator('input:invalid').evaluateAll(nodes=>nodes.map(n=>({id:n.id,error:n.validationMessage}))));throw error; }
    assert((await page.locator('.voucher-panel').first().innerText()).includes('240,50'),'Decimal comma');
    assert.equal(errors.length,0,errors.join('\n'));
    await browser.close();
    console.log('PASS: mobile/desktop no overflow, QR image decoding/search, Kids toggle and real browser create with decimal comma.');
})().catch(e=>{console.error(e);process.exit(1);});

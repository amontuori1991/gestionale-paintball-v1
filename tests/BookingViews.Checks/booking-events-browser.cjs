const {chromium}=require('playwright');
const assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try {
  const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
  for(const width of [390,1440]) {
   await page.setViewportSize({width,height:900});
   const response=await page.goto('http://127.0.0.1:55443/preview/booking-events');assert.equal(response.status(),200);
   assert.equal(await page.locator('#eventi-torneo-title').count(),0);
   assert.equal(await page.locator('.partite-event-band').count(),3);
   assert.equal(await page.locator('.accordion-item[data-section="future"] .partite-event-band').count(),2);
   assert.equal(await page.locator('.accordion-item[data-section="passate"] .partite-event-band').count(),1);
   assert.equal(await page.locator('.accordion-item[data-section="cancellate"] .partite-event-band').count(),0);
   const band=page.locator('[data-event-id="992"]');
   assert.equal(await band.locator('a').getAttribute('href'),'/Torneo/Dettaglio/992');
   const week=band.locator('xpath=ancestor::div[contains(@class,"accordion-item")]');
   assert.equal(await week.locator('.pm-card').count(),0);
   assert.equal((await week.locator('.partite-week-count').innerText()).trim(),'1');
   const date=await band.getAttribute('data-event-date');
   await page.locator('#filtro-data-da').fill(date);await page.locator('#filtro-data-a').fill(date);
   await page.locator('#filtro-data-a').dispatchEvent('change');
   await page.waitForTimeout(450);
   assert(await band.isVisible());
   assert(!(await page.locator('[data-event-id="991"]').isVisible()));
   assert(!(await page.locator('[data-event-id="993"]').isVisible()));
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
   await page.screenshot({path:`.codex-build/vouchers/booking-event-band-${width}.png`,fullPage:true});
   await page.locator('#filtro-stato').selectOption('confermata');
   assert(!(await band.isVisible()));
  }
  assert.deepEqual(errors,[]);
  console.log('PASS event bands in correct future/past weeks, event-only week, full width mobile/desktop, date/status filters, manage link.');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});

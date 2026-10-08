const {chromium}=require('playwright');
const assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try{
  const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
  assert.equal((await page.request.get('http://127.0.0.1:55447/SimulazioneCampi')).status(),401);
  await page.context().addCookies([{name:'VoucherTestRole',value:'Admin',url:'http://127.0.0.1:55447'}]);
  for(const width of [390,1440]){
   await page.setViewportSize({width,height:900});
   const response=await page.goto('http://127.0.0.1:55447/SimulazioneCampi');assert.equal(response.status(),200);
   await page.locator('#use-real').uncheck();
   await page.locator('#date').fill('2030-06-08');await page.locator('#date').dispatchEvent('change');
   await page.locator('#slots button').first().waitFor();
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
   await page.locator('#potential').fill('18');await page.locator('#slots button').first().click();
   assert((await page.locator('#assignment').innerText()).includes('passaggio al torneo'));
   assert.equal(await page.locator('#assign-field').inputValue(),'2');
   await page.screenshot({path:`.codex-build/vouchers/field-simulator-${width}.png`,fullPage:true});
  }
  await page.locator('#reserve').click();assert.equal(await page.locator('#blocks>div').count(),1);
  await page.locator('#field2').uncheck();assert((await page.locator('#blocks').innerText()).includes('ATTENZIONE'));
  await page.locator('#potential').fill('');await page.locator('#people').fill('6');assert.equal(await page.locator('#slots button').count(),0);
  await page.locator('#field2').check();await page.locator('#slots button').first().waitFor();
  await page.locator('#people').fill('18');await page.locator('#slots button').first().click();
  assert.equal(await page.locator('#assign-field').inputValue(),'1');assert(await page.locator('#duration').isDisabled());
  assert((await page.locator('#message').innerText()).includes('90 minuti al prezzo'));
  await page.locator('#date').fill('2030-06-10');await page.locator('#date').dispatchEvent('change');
  await page.waitForFunction(()=>document.getElementById('result').textContent.includes('solo richiesta'));
  assert.equal(await page.locator('#slots button').count(),0);assert(await page.locator('#selection').isHidden());
  assert.equal(await page.locator('#blocks>div').count(),0);
  const bad=await page.request.get('http://127.0.0.1:55447/SimulazioneCampi/Orari?date=invalid');assert.equal(bad.status(),400);
  await page.context().addCookies([{name:'VoucherTestRole',value:'Admin',url:'http://127.0.0.1:55447'}]);
  await page.reload();
  await page.locator('#date').fill('2030-06-08');await page.locator('#date').dispatchEvent('change');
  await page.waitForFunction(()=>document.getElementById('real-status').textContent.includes('1 prenotazioni'));
  assert((await page.locator('#blocks').innerText()).includes('Campo 2'));
  assert((await page.locator('#blocks').innerText()).includes('caparra in attesa'));
  assert.equal(await page.locator('#blocks button').count(),0);
  await page.locator('#field2').uncheck();assert((await page.locator('#blocks').innerText()).includes('Campo 1'));
  await page.locator('#use-real').uncheck();await page.locator('#slots button').first().waitFor();
  assert.equal(await page.locator('#blocks>div').count(),0);
  assert.equal(errors.length,0,errors.join('\n'));
  console.log('PASS: Admin-only simulator, explicit fictitious mode, mobile/desktop, real loading, capacity, closures and weekday stop.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});

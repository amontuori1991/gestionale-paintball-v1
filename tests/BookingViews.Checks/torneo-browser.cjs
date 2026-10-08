const {chromium}=require('playwright');
const assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try {
  const page=await browser.newPage(), errors=[];
  page.on('pageerror', e=>errors.push(e.message));
  const base='http://127.0.0.1:55447';
  assert.equal((await page.request.get(base+'/Torneo')).status(),401);
  await page.context().addCookies([{name:'VoucherTestRole',value:'Staff',url:base}]);
  assert.equal((await page.request.post(base+'/Torneo/Crea',{form:{Nome:'No CSRF'}})).status(),400);
  await page.goto(base+'/Torneo/Crea');
  await page.locator('#Nome').fill('Torneo test Staff');
  await page.locator('#Data').fill('2030-06-08');
  await page.locator('#NumeroSquadre').fill('4');
  await page.locator('#NumeroGironi').fill('2');
  await page.locator('#QualificatePerGirone').fill('2');
  await page.locator('#FinaleTerzoPosto').check();
  await page.getByRole('button',{name:'Crea torneo e squadre'}).click();
  await page.waitForURL('**/Torneo/Dettaglio/*');
  const url=page.url();
  const stale=await page.locator('form[action="/Torneo/GeneraGironi"] input[name="version"]').inputValue();
  assert.equal(await page.locator('#squadre article').count(),4);
  for(const width of [390,1440]) {
   await page.setViewportSize({width,height:900});
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
   await page.screenshot({path:`.codex-build/vouchers/torneo-${width}.png`,fullPage:true});
  }
  const generate=page.locator('form[action="/Torneo/GeneraGironi"]');
  await generate.locator('button').click();
  const generated=page.waitForResponse(r=>r.url().endsWith('/Torneo/GeneraGironi') && r.request().method()==='POST');
  await page.locator('dialog[open] button').filter({hasText:'Conferma'}).click();
  await generated;await page.locator('form[data-result-form]').first().waitFor();
  assert.equal(await page.locator('form[data-result-form]').count(),2);
  const csrf=await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
  const id=url.split('/').pop();
  const staleResponse=await page.request.post(base+'/Torneo/GeneraGironi',{form:{id,version:stale,__RequestVerificationToken:csrf},maxRedirects:0});
  assert.equal(staleResponse.status(),302);
  await page.goto(url);
  assert((await page.locator('body').innerText()).includes('altro operatore'));
  for(let i=0;i<2;i++){
   const form=page.locator('[data-played="false"] form[data-result-form]').first();
   await form.locator('select').selectOption('Casa');
   await form.locator('button').click();
   await page.waitForLoadState('networkidle');
  }
  await page.locator('form[action="/Torneo/GeneraFinali"] button').click();
  const finalized=page.waitForResponse(r=>r.url().endsWith('/Torneo/GeneraFinali') && r.request().method()==='POST');
  await page.locator('dialog[open] button').filter({hasText:'Conferma'}).click();
  await finalized;await page.locator('[data-played="false"] form[data-result-form]').first().waitFor();
  for(let i=0;i<2;i++) {
   const form=page.locator('[data-played="false"] form[data-result-form]').first();
   await form.locator('select').selectOption('Casa');await form.locator('button').click();await page.waitForLoadState('networkidle');
  }
  assert((await page.locator('#incontri').innerText()).includes('Terzo posto'));
  await page.getByRole('link',{name:'Modifica evento'}).click();
  assert(await page.locator('#NumeroGironi').isDisabled());
  await page.locator('#Nome').fill('Torneo aggiornato');await page.getByRole('button',{name:'Salva impostazioni'}).click();
  await page.waitForURL('**/Torneo/Dettaglio/*');assert.equal(await page.locator('h1').innerText(),'Torneo aggiornato');
  await page.locator('details.torneo-danger summary').click();
  await page.getByRole('button',{name:'Elimina definitivamente'}).click();
  await page.locator('dialog[open] button').filter({hasText:'Conferma'}).click();
  await page.waitForURL('**/Torneo');
  assert.equal((await page.request.get(url)).status(),404);
  assert.deepEqual(errors,[]);
  console.log('PASS tournament Staff CRUD, CSRF, concurrency, groups/finals/third place, locked settings, mobile/desktop.');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});

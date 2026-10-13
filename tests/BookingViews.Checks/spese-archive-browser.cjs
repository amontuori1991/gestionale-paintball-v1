const {chromium}=require('playwright');
const assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try {
  const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
  const base='http://127.0.0.1:55443';
  assert.equal((await page.request.get(base+'/Spese')).status(),401);
  await page.context().addCookies([{name:'PhotoTestRole',value:'Staff',url:base}]);
  await page.goto(base+'/Spese');
  const token=await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
  const headers={RequestVerificationToken:token};
  const suffix=Date.now();
  for(const paid of [false,true]){
   const response=await page.request.post(base+'/Spese/Aggiungi',{headers,data:{Data:'2026-10-13',Descrizione:`Test-${suffix}-${paid}`,Importo:paid?20:10,Rimborsato:paid}});
   assert.equal(response.status(),200);
  }
  await page.reload();
  const row=()=>page.locator('#tabellaSpese tbody tr').filter({hasText:`Test-${suffix}-false`});
  await page.waitForFunction(()=>window.jQuery?.fn.dataTable?.isDataTable('#tabellaSpese'));
  assert.equal(await row().count(),1);assert.equal(await page.getByText(`Test-${suffix}-true`,{exact:true}).count(),0);
  const id=await row().getAttribute('data-id');
  assert.equal((await page.request.post(base+'/Spese/AggiornaRimborsato',{data:{Id:Number(id),Rimborsato:true}})).status(),400);
  await row().locator('input[type=checkbox]').click();
  await page.getByText('Confermi di voler archiviare la spesa?',{exact:true}).waitFor();
  await page.locator('.swal2-cancel').click();
  assert(!(await row().locator('input').isChecked()));
  await page.route('**/Spese/AggiornaRimborsato',r=>r.fulfill({status:500,body:'Error'}));
  await row().locator('input').click();await page.locator('.swal2-confirm').click();
  await page.getByText('Qualcosa non va',{exact:true}).waitFor();await page.locator('.swal2-confirm').click();
  assert.equal(await row().count(),1);assert(!(await row().locator('input').isChecked()));
  await page.unroute('**/Spese/AggiornaRimborsato');
  for(const width of [390,1440]){
   await page.setViewportSize({width,height:900});
   await row().locator('input').click();
   await page.getByText('Confermi di voler archiviare la spesa?',{exact:true}).waitFor();
   await page.waitForTimeout(500);
   await page.screenshot({path:`.codex-build/vouchers/spese-confirm-${width}.png`,fullPage:true});
   if(width===390)await page.locator('.swal2-cancel').click();
  }
  await page.locator('.swal2-confirm').click();
  await page.getByText('Spesa rimborsata e archiviata.',{exact:true}).waitFor();await page.locator('.swal2-confirm').click();
  assert.equal(await row().count(),0);
  await page.getByRole('link',{name:'Archivio spese rimborsate',exact:true}).click();
  await row().waitFor();assert.equal(await page.getByText(`Test-${suffix}-true`,{exact:true}).count(),1);
  await row().locator('input').click();await page.locator('.swal2-confirm').click();
  await page.getByText('Spesa ripristinata tra quelle da rimborsare.',{exact:true}).waitFor();await page.locator('.swal2-confirm').click();
  assert.equal(await row().count(),0);
  await page.getByRole('link',{name:'Torna alle spese da rimborsare'}).click();await row().waitFor();
  assert(!(await row().locator('input').isChecked()));assert.deepEqual(errors,[]);
  console.log('PASS existing paid archive, Staff access, CSRF, cancel, failure rollback, archive, restore and styled confirmation desktop/mobile.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});

(() => {
    'use strict';
    const $=id=>document.getElementById(id);
    const engine=window.FieldSimulator;
    let hours=null,selected=null,serial=0,blocks=[],activeByDate={};
    let realData=null,realBlocks=[],realFailed=false;
    const time=n=>`${String(Math.floor(n/60)).padStart(2,'0')}:${String(n%60).padStart(2,'0')}`;
    const minutes=s=>{const [h,m]=s.split(':').map(Number);return h*60+m;};
    const dayNumber=s=>Date.parse(s+'T00:00:00Z')/86400000;
    const onDate=()=>[...realBlocks,...blocks.filter(b=>b.date===$('date').value)];
    const active=()=>activeByDate[$('date').value]||{1:true,2:true};
    const weekday=()=>![0,6].includes(new Date($('date').value+'T12:00:00Z').getUTCDay());
    function drawBlocks(){
        $('blocks').replaceChildren();
        for(const b of onDate()){
            const row=document.createElement('div'),text=document.createElement('span'),remove=document.createElement('button');
            const collision=b.kind==='booking'&&(!active()[b.field]||onDate().some(other=>other!==b&&other.field===b.field&&other.start<b.end&&other.end>b.start));
            const label=b.real?(b.kind==='booking'?`Prenotazione #${b.id}: ${b.type}, ${b.people} persone, caparra ${b.paid?'confermata':'in attesa'}${b.unassigned?' — NON ASSEGNABILE, blocco prudenziale':''}`:'Chiusura reale (entrambi i campi)'):(b.kind==='booking'?'Partita fittizia':'Chiusura');
            text.textContent=`Campo ${b.field} / ${time(b.start)}–${time(b.end)} / ${label}${collision?' — ATTENZIONE: conflitto, risolvere manualmente':''}`;
            remove.textContent='Rimuovi';remove.className='secondary';remove.onclick=()=>{blocks=blocks.filter(x=>x!==b);render();};
            row.append(text);if(!b.real)row.append(remove);$('blocks').append(row);
        }
    }
    function showSelection(slot){
        selected=slot;$('selection').hidden=false;$('assign-field').replaceChildren();
        slot.fields.forEach(f=>{const o=document.createElement('option');o.value=f;o.textContent=`Campo ${f}${f===slot.fields[0]?' (suggerito)':''}`;$('assign-field').append(o);});
        const potential=Number($('potential').value),n=Number($('people').value);
        let change='';
        if(potential>n){
            const next=engine.slots({...parameters(),people:potential});
            const candidate=next.find(x=>x.start===slot.start);
            change=` Massimo potenziale ${potential}: ${candidate?'compatibile ora con Campo '+candidate.fields.join(' / '):'non disponibile nello stesso orario'}. Non garantito: richiede nuova verifica${potential>=17?' e passaggio al torneo (90 minuti)':''}.`;
        }
        $('assignment').textContent=`${time(slot.start)}–${time(slot.end)}. Campi compatibili liberi: ${slot.fields.join(', ')}.${change}`;
        updateMessage();document.querySelectorAll('#slots button').forEach(b=>b.setAttribute('aria-pressed',String(Number(b.dataset.start)===slot.start)));
    }
    function updateMessage(){
        if(!selected)return;
        $('message').textContent=`SIMULAZIONE — NON INVIARE\nRichiesta presso la struttura\nData: ${$('date').value}\nOrario: ${time(selected.start)}–${time(selected.end)}\nGruppo: ${$('type').value}, ${$('people').value} partecipanti\nColpi: ${$('shots').value==='unlimited'?'illimitati':'standard'}\n${Number($('people').value)>=17?'Torneo: 90 minuti al prezzo di un’ora':'Durata: '+(selected.end-selected.start)+' minuti'}\nMassimo potenziale: ${$('potential').value||'non indicato'} (non garantito)\nL’assegnazione del campo spetta alla struttura. Aumenti solo previa verifica; attendere conferma prima di pagare.\n\nNOTA INTERNA: Campo ${$('assign-field').value}. Caparra: una quota del pacchetto, extra esclusi. Nessun prezzo numerico simulato.`;
    }
    function parameters(){return {type:$('type').value,people:Number($('people').value),duration:Number($('duration').value),shots:$('shots').value,blocks:onDate(),active:active(),opening:hours.opening,closing:hours.closing,earliest:$('date').value===hours.today?hours.nowMinutes+240:hours.opening};}
    function render(){
        if(hours&&realData){
            const allocation=engine.allocate(realData.bookings,realData.closures,active(),hours.opening,hours.closing);
            realBlocks=allocation.blocks;
            $('real-status').textContent=`${realData.bookings.length} prenotazioni caricate. ${allocation.unassigned.length} da verificare manualmente. Assegnazione simulata: prima i gruppi vincolati, poi Campo 2 ove possibile. Non e una garanzia di ottimo globale.`;
        }
        selected=null;$('selection').hidden=true;$('slots').replaceChildren();drawBlocks();
        if(!hours)return;
        if(realFailed){$('result').textContent='Dati reali non disponibili: nessuno slot mostrato. Ricarica o passa esplicitamente alla modalita fittizia.';return;}
        const n=Number($('people').value),possible=engine.fields($('type').value,n),potential=Number($('potential').value);
        const tournament=n>=17&&n<=30;
        $('duration').disabled=tournament;
        $('rules').textContent=tournament?'Torneo: durata effettiva 90 minuti, prezzo di un’ora. Solo Campo 1.':`Campi compatibili: ${possible.length?possible.map(f=>'Campo '+f).join(' / '):'nessuno: gestione manuale'}.`;
        if(!Number.isInteger(n)||n<1||n>100||($('potential').value&&(!Number.isInteger(potential)||potential<n||potential>100))){$('result').textContent='Indica partecipanti validi e un massimo potenziale non inferiore al numero concordato.';return;}
        const ahead=dayNumber($('date').value)-dayNumber(hours.today);
        if(ahead<0){$('result').textContent='Data passata: nessuna richiesta disponibile.';return;}
        if(weekday()){
            $('result').textContent=ahead<7?'Infrasettimanale: servono almeno 7 giorni di preavviso.':'Infrasettimanale: solo richiesta da sottoporre allo staff. Nessuno slot confermabile o campo assegnato, anche se libero.';
            return;
        }
        const slots=engine.slots(parameters());
        $('result').textContent=slots.length?`${slots.length} orari simulati. Scegli un orario per vedere assegnazione e bozza.`:'Nessuna soluzione automatica: verifica numero, pacchetto, campi attivi, impegni e preavviso di 4 ore.';
        for(const slot of slots){const b=document.createElement('button');b.textContent=`${time(slot.start)}–${time(slot.end)}`;b.dataset.start=slot.start;b.setAttribute('aria-pressed','false');b.onclick=()=>showSelection(slot);$('slots').append(b);}
    }
    async function loadDate(){
        const request=++serial;hours=null;selected=null;realData=null;realBlocks=[];realFailed=false;
        const useReal=$('use-real')?.checked;
        if($('real-status'))$('real-status').textContent=useReal?'Caricamento prenotazioni...':'Solo dati fittizi.';
        $('selection').hidden=true;$('slots').replaceChildren();$('result').textContent='Calcolo degli orari...';$('hours').textContent='';
        $('field1').checked=active()[1];$('field2').checked=active()[2];drawBlocks();
        try{
            const response=await fetch('/SimulazioneCampi/Orari?date='+encodeURIComponent($('date').value));
            if(!response.ok)throw new Error();const data=await response.json();if(request!==serial)return;
            if(useReal){
                try{
                    const responseReal=await fetch('/SimulazioneCampi/Prenotazioni?date='+encodeURIComponent($('date').value));
                    if(!responseReal.ok||!responseReal.headers.get('content-type')?.includes('application/json'))throw new Error();
                    const snapshot=await responseReal.json();if(request!==serial)return;realData=snapshot;
                }catch{if(request!==serial)return;realFailed=true;$('real-status').textContent='Caricamento fallito. Verifica accesso Admin e connessione.';}
            }
            hours=data;$('hours').textContent=`Apertura ${time(data.opening)} · Tramonto ${data.sunset} · Ultima fine partita ${time(data.closing)}`;render();
        }catch{if(request===serial)$('result').textContent='Impossibile calcolare gli orari. Controlla la data o riprova.';}
    }
    $('date').value=new Intl.DateTimeFormat('en-CA',{timeZone:'Europe/Rome',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
    $('date').addEventListener('change',loadDate);
    $('use-real')?.addEventListener('change',loadDate);
    $('reload-real')?.addEventListener('click',loadDate);
    ['people','potential','duration','shots'].forEach(id=>$(id).addEventListener('input',render));
    $('type').onchange=()=>{$('shots').value=$('type').value==='Kids'?'unlimited':'standard';$('shots').disabled=$('type').value==='Kids';render();};
    [1,2].forEach(f=>$('field'+f).onchange=()=>{activeByDate[$('date').value]={1:$('field1').checked,2:$('field2').checked};render();});
    $('assign-field').onchange=updateMessage;
    $('reserve').onclick=()=>{if(!selected)return;blocks.push({date:$('date').value,field:Number($('assign-field').value),start:selected.start,end:selected.end,kind:'booking'});render();};
    $('block-form').onsubmit=e=>{
        e.preventDefault();const start=minutes($('block-start').value),end=minutes($('block-end').value);$('block-error').textContent='';
        if(!hours||!Number.isFinite(start)||!Number.isFinite(end)||end<=start){$('block-error').textContent='Seleziona una data valida e un orario finale successivo all’inizio.';return;}
        blocks.push({date:$('date').value,field:Number($('block-field').value),start,end,kind:$('block-kind').value});render();
    };
    $('reset').onclick=()=>{blocks=[];activeByDate={};$('field1').checked=true;$('field2').checked=true;$('block-error').textContent='';render();};
    loadDate();
})();

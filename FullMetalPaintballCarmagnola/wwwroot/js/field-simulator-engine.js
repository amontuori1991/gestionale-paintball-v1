(function(root){
    'use strict';
    function fields(type,n){
        if(!Number.isInteger(n)||n<1||n>30||!['Adulti','Kids'].includes(type))return [];
        return [n>=8?1:null,(type==='Adulti'?n>=6&&n<=10:n>=8&&n<=14)?2:null].filter(Boolean);
    }
    function free(field,start,end,blocks,active,opening,closing){
        return active[field]!==false && start>=opening && end<=closing && !blocks.some(b=>{
            const margin=b.kind==='booking'?30:0;
            let from=b.start-margin;
            if(b.kind==='booking' && from-opening<60)from=opening;
            return b.field===field && start<b.end+margin && end>from;
        });
    }
    function slots({type,people,duration,shots,blocks,active,opening,closing,earliest=opening}){
        const compatible=fields(type,people);
        const actual=people>=17&&people<=30?90:duration;
        if(![60,90,120].includes(actual)||(! (people>=17) && type==='Adulti'&&shots==='unlimited'&&actual===120))return [];
        const result=[];
        for(let start=Math.ceil(Math.max(opening,earliest)/30)*30;start+actual<=closing;start+=30){
            const available=[2,1].filter(f=>compatible.includes(f)&&free(f,start,start+actual,blocks,active,opening,closing));
            if(available.length)result.push({start,end:start+actual,fields:available});
        }
        return result;
    }
    function allocate(bookings,closures,active,opening,closing){
        const fixed=closures.flatMap(c=>[1,2].map(field=>({...c,field,kind:'closure',real:true})));
        const sorted=[...bookings].sort((a,b)=>fields(a.type,a.people).length-fields(b.type,b.people).length||a.start-b.start||a.id-b.id);
        const placed=[],unassigned=[];
        // Constrained groups first; flexible groups preserve Campo 1 where possible.
        for(const b of sorted){
            const candidates=fields(b.type,b.people);
            const valid=Number.isFinite(b.start)&&Number.isFinite(b.end)&&b.end>b.start;
            const field=valid?[2,1].find(f=>candidates.includes(f)&&free(f,b.start,b.end,[...fixed,...placed],active,opening,closing)):null;
            const row={...b,kind:'booking',real:true};
            if(field)placed.push({...row,field});
            else unassigned.push({...row,start:valid?b.start:opening,end:valid?b.end:closing});
        }
        // Never advertise a free slot by silently discarding an incompatible real booking.
        const blocked=unassigned.flatMap(b=>[1,2].map(field=>({...b,field,unassigned:true})));
        return {blocks:[...fixed,...placed,...blocked],unassigned};
    }
    const api={fields,free,slots,allocate};
    if(typeof module!=='undefined'&&module.exports)module.exports=api;
    else root.FieldSimulator=api;
})(typeof window!=='undefined'?window:globalThis);

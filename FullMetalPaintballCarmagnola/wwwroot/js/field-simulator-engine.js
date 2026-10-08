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
    const api={fields,free,slots};
    if(typeof module!=='undefined'&&module.exports)module.exports=api;
    else root.FieldSimulator=api;
})(typeof window!=='undefined'?window:globalThis);

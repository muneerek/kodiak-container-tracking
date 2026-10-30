export function localBusinessDate(now = new Date()) { return [now.getFullYear(),String(now.getMonth()+1).padStart(2,'0'),String(now.getDate()).padStart(2,'0')].join('-'); }
export function overviewApi(request) { return { get: (date = localBusinessDate()) => request('/dashboard/overview?date='+encodeURIComponent(date)) }; }
export function attentionReason(item,today) {
 if(item.isOnHold)return {title:'Shipment on hold',description:'Review the hold and record the next operational update.'};
 if(item.expectedArrival < today)return {title:'Arrival date passed',description:'Confirm the latest location and update the expected arrival.'};
 return {title:'Awaiting container allocation',description:'Allocate a physical container number to this registration.'};
}
export function distributionSegments(stages,total) {
 const colors={'1':'#9fb4c5','2':'#839cd2','3':'#219ac6','4':'#46a7a3','5':'#9a86bc','6':'#49a58d',hold:'#d5a14e',cancelled:'#c7d0d8'};
 if(!total)return '#e9eff3';let progress=0;
 return 'conic-gradient('+stages.map(stage=>{const start=progress;progress+=stage.count/total*100;return (colors[stage.key]||'#9fb4c5')+' '+start+'% '+progress+'%';}).join(',')+')';
}

export function overviewCopy(userType) {
 if(userType===3)return {title:'Your cargo. Your clear view.',description:'Register containers and follow every milestone in your shipments.',role:'Supplier workspace',shipmentDescription:'Current status of your container registrations',recentDescription:'Your latest shipments and their progress'};
 if(userType===2)return {title:'Your network, connected.',description:'Follow your linked shippers and the shipments that move your business.',role:'Distributor workspace',shipmentDescription:'Current status across your linked shipment network',recentDescription:'Latest shipments from your connected shippers'};
 return {title:'A clear view. A smoother journey.',description:'Every container, partner, and milestone. All in one place.',role:'Administrator workspace',shipmentDescription:'Current status across all registrations',recentDescription:'Your latest registrations and their progress'};
}

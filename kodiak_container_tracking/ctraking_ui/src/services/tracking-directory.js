import { trackingStatusName } from './container-tracking.js';
export function journeyStatus(item) {
  if (item.status === 2) return {name:'Cancelled',tone:'gray'};
  if (item.isOnHold) return {name:'On hold',tone:'amber'};
  return {name:trackingStatusName(item.trackingStatus),tone:item.trackingStatus === 6 ? 'green' : item.trackingStatus === 1 ? 'gray' : 'blue'};
}
export function journeyProgress(item) { return item.status === 2 ? 0 : Math.max(1,Math.min(6,item.trackingStatus || 1)); }
export function trackingFilters(search, status, shipperId) {
  const result = {search:search.trim(),shipperId};
  if (status === 'hold') { result.onHold = true; result.status = 1; }
  else if (status === 'cancelled') result.status = 2;
  else if (status) { result.trackingStatus = Number(status); result.status = 1; }
  return result;
}
export async function fetchTrackingExport(api, filters) {
  const items=[];let page=1;
  while(true) { const result=await api.trackingList(page++,100,filters);items.push(...result.items);if(!result.items.length || items.length >= result.totalCount) return items; }
}

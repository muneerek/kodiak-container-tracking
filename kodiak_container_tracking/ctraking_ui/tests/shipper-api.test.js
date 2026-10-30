import { test } from 'node:test';
import assert from 'node:assert/strict';
import { shipperApi } from '../src/services/shipper-api.js';
test('shipper changes and logo requests use matching IDs and concurrency versions',async()=>{
 const calls=[];const api=shipperApi(async(path,options)=>{calls.push({path,options});return {uniqueId:8};});
 await api.list(1,25);await api.get(8);await api.update(8,'2',{companyName:'Company'});await api.remove(8,'3');await api.uploadLogo(8,'4',new Blob(['logo'],{type:'image/jpeg'}));await api.removeLogo(8,'5');
 assert.equal(calls[0].path,'/shippers?page=1&pageSize=25');assert.equal(calls[1].path,'/shippers/8');assert.equal(calls[2].options.method,'PUT');assert.equal(calls[2].options.version,'2');assert.equal(calls[3].options.method,'DELETE');assert.equal(calls[4].path,'/shippers/8/logo');assert.equal(calls[4].options.version,'4');assert.ok(calls[4].options.body instanceof FormData);assert.equal(calls[5].options.version,'5');
});

test('shipper directory search preserves paging and encodes query text', async () => {
 const api = shipperApi(async path => { assert.equal(path, '/shippers?page=2&pageSize=10&search=Atlas%20%26%20Co'); return {items:[], totalCount:0}; });
 await api.list(2, 10, 'Atlas & Co');
});

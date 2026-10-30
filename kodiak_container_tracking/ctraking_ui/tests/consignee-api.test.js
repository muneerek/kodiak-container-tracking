import { test } from 'node:test';
import assert from 'node:assert/strict';
import { consigneeApi } from '../src/services/consignee-api.js';
test('consignee CRUD uses API routes, versions, and paged country lookup',async()=>{
 const calls=[];let countryPage=0;const api=consigneeApi(async(path,options)=>{calls.push({path,options});return path.startsWith('/masters/Countries') ? {items:[{id:++countryPage}],totalCount:2} : {uniqueId:8};});
 await api.list(2,10);await api.get(8);await api.create({companyName:'Customer'});await api.update(8,'2',{companyName:'Updated'});await api.remove(8,'3');const countries=await api.countries();
 assert.equal(calls[0].path,'/consignees?page=2&pageSize=10');assert.equal(calls[1].path,'/consignees/8');assert.equal(calls[2].options.method,'POST');assert.equal(calls[3].options.method,'PUT');assert.equal(calls[3].options.version,'2');assert.equal(calls[4].options.method,'DELETE');assert.equal(calls[4].options.version,'3');assert.deepEqual(countries,[{id:1},{id:2}]);
});

test('consignee directory search preserves paging and encodes query text', async () => {
 const api = consigneeApi(async path => { assert.equal(path, '/consignees?page=2&pageSize=10&search=Atlas%20%26%20Co'); return {items:[], totalCount:0}; });
 await api.list(2, 10, 'Atlas & Co');
});

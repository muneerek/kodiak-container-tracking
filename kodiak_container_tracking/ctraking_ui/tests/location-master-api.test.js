import { test } from 'node:test';
import assert from 'node:assert/strict';
import { locationMasterApi } from '../src/services/location-master-api.js';
test('country, state and port CRUD preserve kinds and concurrency versions',async()=>{
 for(const kind of ['Countries','States','ShippingPorts']) {
  const calls=[];const api=locationMasterApi(kind,async(path,options)=>{calls.push({path,options});return {id:4};});
  await api.list(2,10);await api.get(4);await api.create({code:'CODE',name:'Name'});await api.update(4,'2',{code:'CODE',name:'Updated'});await api.remove(4,'3');
  assert.equal(calls[0].path,'/masters/'+kind+'?page=2&pageSize=10');assert.equal(calls[1].path,'/masters/'+kind+'/4');assert.equal(calls[2].options.method,'POST');assert.equal(calls[3].options.version,'2');assert.equal(calls[4].options.method,'DELETE');assert.equal(calls[4].options.version,'3');
 }
 assert.throws(()=>locationMasterApi('Unknown',()=>{}));
});
test('location selectors load all pages of master data',async()=>{
 let page=0;const api=locationMasterApi('ShippingPorts',async path=>{assert.ok(path.startsWith('/masters/States?'));return {items:[{id:++page}],totalCount:2};});assert.deepEqual(await api.lookup('States'),[{id:1},{id:2}]);
});

test('timezone autocomplete loads the complete directory', async () => {
 const zones = [{name:'Asia/Kolkata', abbreviation:'IST', utcOffsetSeconds:19800}, {name:'UTC', abbreviation:'UTC', utcOffsetSeconds:0}];
 const api = locationMasterApi('ShippingPorts', async path => { assert.equal(path, '/masters/timezones'); return zones; });
 assert.deepEqual(await api.timeZones(), zones);
});

test('new shipping port fetches its code before saving', async () => {
 const api = locationMasterApi('ShippingPorts', async path => { assert.equal(path, '/masters/ShippingPorts/next-code'); return {code:'PORT-000003'}; });
 assert.deepEqual(await api.nextPortCode(), {code:'PORT-000003'});
});

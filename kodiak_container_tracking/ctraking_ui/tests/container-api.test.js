import { test } from 'node:test';
import assert from 'node:assert/strict';
import { containerApi } from '../src/services/container-api.js';
test('container registration and attachment mutations preserve versioned routes',async()=>{
 const calls=[];const api=containerApi(async(path,options)=>{calls.push({path,options});return {id:7};});
 await api.list(2,10,'KD & 1');await api.get(7);await api.create({containerType:'40HC'});await api.update(7,'2',{cargo:'Cargo'});await api.cancel(7,'3');
 const file=new Blob(['reference'],{type:'application/pdf'});Object.defineProperty(file,'name',{value:'reference.pdf'});await api.upload(7,'4',file,2);await api.removeFile(7,9,'5');await api.download(7,9);
 assert.ok(calls[0].path.endsWith('search=KD%20%26%201'));assert.equal(calls[2].options.method,'POST');assert.equal(calls[3].options.version,'2');assert.equal(calls[4].options.method,'DELETE');assert.equal(calls[5].path,'/containers/7/files');assert.equal(calls[5].options.body.get('kind'),'2');assert.equal(calls[5].options.body.get('file').name,'reference.pdf');assert.equal(calls[6].options.version,'5');assert.equal(calls[7].options.responseType,'blob');
});
test('container lookups include all pages for active port choices',async()=>{
 let page=0;const api=containerApi(async path=>{assert.ok(path.startsWith('/containers/options/ports'));return {items:[{id:++page}],totalCount:2};});assert.deepEqual(await api.options('ports'),[{id:1},{id:2}]);
});

import { containerCsv, fetchContainerExport } from '../src/services/container-export.js';
test('status filter and search are preserved across every export page', async () => {
 const calls=[]; const api=containerApi(async path => {calls.push(path);return {items:[{id:calls.length}],totalCount:2};});
 assert.deepEqual(await fetchContainerExport(api,'Mumbai & Dubai','2'),[{id:1},{id:2}]);
 assert.equal(calls.length,2);assert.ok(calls[0].includes('page=1&pageSize=100'));assert.ok(calls[1].includes('page=2&pageSize=100'));
 for(const path of calls)assert.ok(path.endsWith('search=Mumbai%20%26%20Dubai&status=2'));
});
test('CSV preserves commas, quotes, newlines and protects spreadsheet formula cells',()=>{
 const csv=containerCsv([{code:'KD-1',shipperName:'Company, "Name"',cargo:'Line 1\nLine 2',reference:' =HYPERLINK("example")',status:1}]);
 assert.ok(csv.startsWith('\uFEFF'));assert.ok(csv.includes('"Company, ""Name"""'));assert.ok(csv.includes('"Line 1\nLine 2"'));assert.ok(csv.includes("\"' =HYPERLINK(\"\"example\"\")\""));assert.ok(csv.includes('"Booked"'));
 assert.equal(containerCsv([]).split('\r\n').length,1);
});

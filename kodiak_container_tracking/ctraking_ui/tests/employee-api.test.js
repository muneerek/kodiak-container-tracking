import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createApiClient } from '../src/services/http-client.js';
import { employeeApi } from '../src/services/employee-api.js';
test('updates and deletes send quoted If-Match and bearer authorization', async()=>{
 const original=globalThis.fetch;const calls=[];
 globalThis.fetch=async(url,options)=>{calls.push({url,options});return Response.json({uniqueId:7});};
 try {const api=employeeApi(createApiClient(()=> 'test-access',()=>{}));await api.update(7,'2',{firstName:'Test'});await api.remove(7,'3');assert.equal(calls[0].options.headers['If-Match'],'"2"');assert.equal(calls[1].options.headers['If-Match'],'"3"');assert.equal(calls[1].options.method,'DELETE');assert.equal(calls[1].options.headers.Authorization,'Bearer test-access');} finally {globalThis.fetch=original;}
});
test('unauthorized responses clear the session and conflicts are actionable',async()=>{
 const original=globalThis.fetch;let expired=false;const request=createApiClient(()=> 'test',()=>{expired=true;});
 try {globalThis.fetch=async()=>Response.json({}, {status:401});await assert.rejects(request('/employees'),error=>error.status===401);assert.equal(expired,true);globalThis.fetch=async()=>Response.json({}, {status:412});await assert.rejects(request('/employees/7'),error=>error.message.includes('another user'));} finally {globalThis.fetch=original;}
});
test('master dropdowns load all pages',async()=>{
 let pages=0;const api=employeeApi(async()=>({items:[{id:++pages}],totalCount:2}));assert.deepEqual(await api.masters('Designations'),[{id:1},{id:2}]);
});

test('photo uploads use multipart data and authorized reads return blobs',async()=>{
 const original=globalThis.fetch;const calls=[];
 globalThis.fetch=async(url,options)=>{calls.push({url,options});return options.method==='GET' ? new Response(new Blob(['photo'],{type:'image/jpeg'})) : Response.json({version:'3'});};
 try {const api=employeeApi(createApiClient(()=> 'test-access',()=>{}));const photo=new Blob(['photo'],{type:'image/jpeg'});await api.uploadPhoto(7,'2',photo);assert.ok(calls[0].options.body instanceof FormData);assert.equal(calls[0].options.headers['Content-Type'],undefined);assert.equal(calls[0].options.headers['If-Match'],'"2"');assert.equal(calls[0].options.body.get('photo').name,'photo.jpg');assert.equal(await (await api.photo(7)).text(),'photo');assert.equal(calls[1].options.headers.Authorization,'Bearer test-access');} finally {globalThis.fetch=original;}
});

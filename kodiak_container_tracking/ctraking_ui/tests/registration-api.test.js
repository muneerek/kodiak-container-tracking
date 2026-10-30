import { test } from 'node:test';
import assert from 'node:assert/strict';
import { registrationApi } from '../src/services/registration-api.js';
test('registration is anonymous and sends the required public contract',async()=>{
 const original=globalThis.fetch;const body={companyName:'Test',userName:'test.shipper',password:'test-only-password',confirmPassword:'test-only-password',consigneeId:4};
 globalThis.fetch=async(url,options)=>{assert.equal(url,'/api/registration/shippers');assert.equal(options.method,'POST');assert.equal(options.headers.Authorization,undefined);assert.deepEqual(JSON.parse(options.body),body);return Response.json({userName:'test.shipper',uniqueCode:'SHP-000001'},{status:201});};
 try {assert.equal((await registrationApi.register(body)).uniqueCode,'SHP-000001');} finally {globalThis.fetch=original;}
});
test('registration option requests encode search and load all pages',async()=>{
 const original=globalThis.fetch;let page=0;
 try {globalThis.fetch=async(url)=>{assert.ok(url.includes('search=A%26B'));return Response.json({items:[],totalCount:0});};await registrationApi.options('consignees','A&B');globalThis.fetch=async()=>Response.json({items:[{id:++page}],totalCount:2});assert.deepEqual(await registrationApi.allOptions('countries'),[{id:1},{id:2}]);} finally {globalThis.fetch=original;}
});
test('validation and rate limits are actionable while server details stay private',async()=>{
 const original=globalThis.fetch;
 try {globalThis.fetch=async()=>Response.json({detail:'Choose another username.'},{status:409});await assert.rejects(registrationApi.register({}),error=>error.status===409&&error.message==='Choose another username.');globalThis.fetch=async()=>Response.json({detail:'internal exception'},{status:500});await assert.rejects(registrationApi.register({}),error=>!error.message.includes('internal'));globalThis.fetch=async()=>new Response(null,{status:429});await assert.rejects(registrationApi.register({}),error=>error.message.includes('Wait a minute'));} finally {globalThis.fetch=original;}
});

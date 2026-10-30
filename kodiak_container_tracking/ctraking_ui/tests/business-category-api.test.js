import { test } from 'node:test';
import assert from 'node:assert/strict';
import { businessCategoryApi } from '../src/services/business-category-api.js';
test('category actions use master routes and carry versions for edits and deletion',async()=>{
 const calls=[];const api=businessCategoryApi(async(path,options)=>{calls.push({path,options});return {id:4};});
 await api.list(2,10);await api.get(4);await api.create({code:'EXPORT',name:'Export',isActive:true});await api.update(4,'2',{code:'EXPORT',name:'Updated',isActive:true});await api.remove(4,'3');
 assert.equal(calls[0].path,'/masters/BusinessCategories?page=2&pageSize=10');assert.equal(calls[1].path,'/masters/BusinessCategories/4');assert.equal(calls[2].options.method,'POST');assert.equal(calls[3].options.method,'PUT');assert.equal(calls[3].options.version,'2');assert.equal(calls[4].options.method,'DELETE');assert.equal(calls[4].options.version,'3');
});

import test from 'node:test';
import assert from 'node:assert/strict';
import {validateImageUpload,fitImageRect} from '../src/services/image-upload.js';
test('accepts supported images within the upload limit',()=>{
 for(const type of ['image/jpeg','image/png','image/webp'])assert.equal(validateImageUpload({type,size:10*1024*1024}),'');
});
test('rejects unsupported, empty and oversized uploads',()=>{
 for(const file of [null,{type:'application/pdf',size:100},{type:'image/png',size:0},{type:'image/jpeg',size:10*1024*1024+1}])assert.ok(validateImageUpload(file));
});
test('fits full landscape and portrait logos without cropping',()=>{
 assert.deepEqual(fitImageRect(1000,500),{x:0,y:128,width:512,height:256});
 assert.deepEqual(fitImageRect(500,1000),{x:128,y:0,width:256,height:512});
 assert.deepEqual(fitImageRect(500,500),{x:0,y:0,width:512,height:512});
 assert.throws(()=>fitImageRect(0,100));
});

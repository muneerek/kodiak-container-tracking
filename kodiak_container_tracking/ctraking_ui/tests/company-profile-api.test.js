import {test} from 'node:test';
import assert from 'node:assert/strict';
import {companyProfileApi} from '../src/services/company-profile-api.js';
test('profile reads and writes always use the authenticated company endpoint',async()=>{
 const calls=[];const api=companyProfileApi(async(path,options)=>{calls.push({path,options});return {};});
 await api.get();await api.update('4',{companyName:'Updated company'});
 assert.equal(calls[0].path,'/profile/company');assert.equal(calls[1].path,'/profile/company');assert.equal(calls[1].options.method,'PUT');assert.equal(calls[1].options.version,'4');assert.equal(calls[1].options.body.companyName,'Updated company');
});

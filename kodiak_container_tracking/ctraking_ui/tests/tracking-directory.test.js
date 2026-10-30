import {test} from 'node:test';
import assert from 'node:assert/strict';
import {containerApi} from '../src/services/container-api.js';
import {journeyStatus,journeyProgress,trackingFilters,fetchTrackingExport} from '../src/services/tracking-directory.js';
test('journey labels and progress reflect hold, cancellation and delivered milestones',()=>{
 assert.deepEqual(journeyStatus({status:1,trackingStatus:3}),{name:'In transit',tone:'blue'});
 assert.equal(journeyStatus({status:1,trackingStatus:3,isOnHold:true}).name,'On hold');
 assert.equal(journeyStatus({status:2,trackingStatus:3,isOnHold:true}).name,'Cancelled');
 assert.equal(journeyStatus({status:1,trackingStatus:6}).tone,'green');assert.equal(journeyProgress({status:1,trackingStatus:6}),6);
 assert.equal(journeyProgress({status:2,trackingStatus:3}),0);assert.equal(journeyProgress({status:1}),1);
});
test('tracking status filters keep on-hold and cancelled registrations distinct',()=>{
 assert.deepEqual(trackingFilters('  MSCU  ','3',7),{search:'MSCU',shipperId:7,status:1,trackingStatus:3});
 assert.deepEqual(trackingFilters('','hold',''),{search:'',shipperId:'',onHold:true,status:1});
 assert.deepEqual(trackingFilters('','cancelled',''),{search:'',shipperId:'',status:2});
});
test('tracking list and export preserve every filter across pages',async()=>{
 const calls=[];const api=containerApi(async path=>{calls.push(path);return {items:[{id:calls.length}],totalCount:2};});
 const items=await fetchTrackingExport(api,trackingFilters('Atlas & Co','hold',7));assert.equal(items.length,2);
 for(const [index,path] of calls.entries()){
  const url=new URL(path,'http://localhost');assert.equal(url.pathname,'/containers/tracking');
  assert.equal(url.searchParams.get('page'),String(index+1));assert.equal(url.searchParams.get('search'),'Atlas & Co');
  assert.equal(url.searchParams.get('onHold'),'true');assert.equal(url.searchParams.get('status'),'1');assert.equal(url.searchParams.get('shipperId'),'7');
 }
});

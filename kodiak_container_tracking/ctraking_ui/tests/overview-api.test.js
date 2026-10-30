import {test} from 'node:test';
import assert from 'node:assert/strict';
import {overviewApi,localBusinessDate,attentionReason,distributionSegments} from '../src/services/overview-api.js';
test('overview supplies the local business date rather than a UTC date',async()=>{
 const now=new Date(2026,9,26,0,15);assert.equal(localBusinessDate(now),'2026-10-26');
 const api=overviewApi(async path=>{assert.equal(path,'/dashboard/overview?date=2026-10-26');return {counts:{total:6}};});
 assert.equal((await api.get('2026-10-26')).counts.total,6);
});
test('attention reasons prioritize holds, overdue arrival, then allocation',()=>{
 assert.equal(attentionReason({isOnHold:true,expectedArrival:'2026-10-20'},'2026-10-26').title,'Shipment on hold');
 assert.equal(attentionReason({expectedArrival:'2026-10-20'},'2026-10-26').title,'Arrival date passed');
 assert.equal(attentionReason({expectedArrival:'2026-11-10'},'2026-10-26').title,'Awaiting container allocation');
});
test('distribution shows real proportions and handles an empty dashboard',()=>{
 assert.equal(distributionSegments([],0),'#e9eff3');
 const result=distributionSegments([{key:'3',count:3},{key:'6',count:1}],4);assert.ok(result.includes('0% 75%'));assert.ok(result.includes('75% 100%'));
});

import {overviewCopy} from '../src/services/overview-api.js';
test('shipper and consignee dashboards have role-specific language',()=>{
 assert.equal(overviewCopy(3).title,'Your cargo. Your clear view.');assert.equal(overviewCopy(3).role,'Supplier workspace');
 assert.equal(overviewCopy(2).title,'Your network, connected.');assert.equal(overviewCopy(2).role,'Distributor workspace');
 assert.equal(overviewCopy(1).role,'Administrator workspace');
});

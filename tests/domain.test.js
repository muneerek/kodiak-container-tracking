import test from 'node:test';
import assert from 'node:assert/strict';
import { initialData, demoAccounts, visibleContainers, visibleSuppliers, canBook, containerNumberIssue, isOverdue } from '../src/data.js';

test('each role sees the correct supplier and container scope', () => {
  const d=initialData();
  const [admin,distributor,supplier]=demoAccounts;
  assert.equal(visibleContainers(d,admin).length,8);
  assert.equal(visibleContainers(d,distributor).length,5);
  assert.equal(visibleContainers(d,supplier).length,3);
  assert(visibleContainers(d,distributor).every(c=>c.distributorId==='d1'));
  assert(visibleSuppliers(d,distributor).every(s=>s.distributorId==='d1'));
  assert.equal(visibleContainers(d,null).length,0);
});
test('pending and blacklisted suppliers cannot book, but retain existing tracking',()=>{
  const d=initialData();
  assert.equal(canBook(d,'s1'),true);
  assert.equal(canBook(d,'s4'),false);
  assert.equal(canBook(d,'s5'),false);
  assert.equal(visibleContainers(d,{role:'supplier',org:'s4'}).length,1);
  d.suppliers.find(s=>s.id==='s1').state='Blacklisted';
  assert.equal(canBook(d,'s1'),false);
  assert.equal(visibleContainers(d,demoAccounts[2]).length,3);
});
test('physical number validation normalizes identifiers and prevents duplicate or blacklisted allocation',()=>{
  const d=initialData();
  assert.match(containerNumberIssue(d,'mscu7823416'),/active journey/);
  assert.equal(containerNumberIssue(d,'MSCU 782341-6','KD-2048'),'');
  assert.equal(containerNumberIssue(d,'Pending allocation'),'');
  assert.equal(containerNumberIssue(d,'FCIU9120478'),'');
  d.containers.find(c=>c.id==='KD-2044').blacklisted=true;
  assert.match(containerNumberIssue(d,'FCIU 912047-8'),/blacklisted/);
});
test('arrival overdue excludes arrivals and delivered journeys',()=>{
  const d=initialData();
  assert.equal(isOverdue(d.containers.find(c=>c.id==='KD-2042'),'2026-10-03'),true);
  assert.equal(isOverdue(d.containers.find(c=>c.id==='KD-2046'),'2026-10-03'),false);
  assert.equal(isOverdue(d.containers.find(c=>c.id==='KD-2044'),'2026-10-03'),false);
});
test('submitted container distributor scope survives changes to supplier membership',()=>{
  const d=initialData();
  d.suppliers.find(s=>s.id==='s1').distributorId='d2';
  assert.equal(visibleContainers(d,demoAccounts[1]).length,5);
  assert(!visibleSuppliers(d,demoAccounts[1]).some(s=>s.id==='s1'));
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { authRequest } from '../src/services/auth-api.js';
const session = { accessToken: 'test-access', refreshToken: 'test-refresh', expiresOn: '2030-01-01T00:00:00Z', user: { userName: 'test' } };
test('login sends the API contract without a Bearer header', async () => {
  const original=globalThis.fetch;
  globalThis.fetch=async (url,options)=>{ assert.equal(url,'/api/auth/login'); assert.deepEqual(JSON.parse(options.body),{userName:'test',password:'not-a-real-password'}); assert.equal(options.headers.Authorization,undefined); return Response.json(session); };
  try { assert.deepEqual(await authRequest('login',{userName:'test',password:'not-a-real-password'}),session); } finally { globalThis.fetch=original; }
});
test('errors give safe actionable messages instead of server internals', async () => {
  const original=globalThis.fetch;
  try { for(const status of [400,401,429,500]) {globalThis.fetch=async()=>Response.json({detail:'internal database details'},{status}); await assert.rejects(authRequest('login',{}),error=>error.status===status && !error.message.includes('internal database'));} } finally {globalThis.fetch=original;}
});
test('logout sends the Bearer token and accepts an empty response',async()=>{
  const original=globalThis.fetch;globalThis.fetch=async(url,options)=>{assert.equal(options.headers.Authorization,'Bearer test-access');return new Response(null,{status:204});};
  try {assert.equal(await authRequest('logout',undefined,'test-access'),null);} finally {globalThis.fetch=original;}
});
test('network failures and invalid login responses are handled',async()=>{
 const original=globalThis.fetch;
 try {globalThis.fetch=async()=>{throw new TypeError('fetch failed');};await assert.rejects(authRequest('login',{}),error=>error.status===0);globalThis.fetch=async()=>Response.json({});await assert.rejects(authRequest('login',{}),error=>error.status===502);} finally {globalThis.fetch=original;}
});

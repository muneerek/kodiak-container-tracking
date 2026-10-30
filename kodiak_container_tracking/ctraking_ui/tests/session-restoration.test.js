import { test } from 'node:test';
import assert from 'node:assert/strict';

test('reload restores through refresh, rotates storage, and logout removes it', async () => {
  const originalFetch = globalThis.fetch;
  const descriptor = Object.getOwnPropertyDescriptor(globalThis, 'sessionStorage');
  const values = new Map(); const controllers = []; const calls = [];
  Object.defineProperty(globalThis, 'sessionStorage', { configurable: true, value: { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value), removeItem: key => values.delete(key) } });
  const result = token => ({accessToken:'access-'+token,refreshToken:token,expiresOn:new Date(Date.now()+900000).toISOString(),user:{userName:'test.user'}});
  globalThis.fetch = async (url, options) => { calls.push(url); return url.endsWith('/logout') ? new Response(null,{status:204}) : Response.json(result(url.endsWith('/login') ? 'refresh-one' : 'refresh-two')); };
  try {
    const first = (await import('../src/composables/useAuth.js?restoration-test=first')).useAuth(); controllers.push(first);
    await first.restoreSession(); assert.equal(calls.length,0); assert.equal(first.restoring.value,false);
    await first.signIn('test.user','test-password'); assert.deepEqual([...values.values()],['refresh-one']);
    const reloaded = (await import('../src/composables/useAuth.js?restoration-test=reload')).useAuth(); controllers.push(reloaded);
    await Promise.all([reloaded.restoreSession(),reloaded.restoreSession()]);
    assert.equal(calls.filter(url=>url.endsWith('/refresh')).length,1); assert.equal(reloaded.session.value.user.userName,'test.user'); assert.deepEqual([...values.values()],['refresh-two']);
    await reloaded.signOut(); assert.equal(values.size,0); assert.equal(reloaded.session.value,null);
    values.set('kodiak.refresh-token','invalid-token'); globalThis.fetch=async()=>Response.json({}, {status:401});
    const expired=(await import('../src/composables/useAuth.js?restoration-test=expired')).useAuth(); controllers.push(expired); await expired.restoreSession();
    assert.equal(values.size,0); assert.equal(expired.session.value,null); assert.equal(expired.restoring.value,false);
    values.set('kodiak.refresh-token','retry-token'); globalThis.fetch=async()=>{throw new TypeError('offline');};
    const offline=(await import('../src/composables/useAuth.js?restoration-test=offline')).useAuth(); controllers.push(offline); await offline.restoreSession();
    assert.equal(values.get('kodiak.refresh-token'),'retry-token'); assert.equal(offline.session.value,null); assert.equal(offline.restoring.value,false);
  } finally {
    globalThis.fetch=async()=>new Response(null,{status:204}); for(const controller of controllers) await controller.signOut();
    globalThis.fetch=originalFetch; if(descriptor) Object.defineProperty(globalThis,'sessionStorage',descriptor); else delete globalThis.sessionStorage;
  }
});

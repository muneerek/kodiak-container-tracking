const key = 'kodiak.refresh-token';
// Persist only the refresh token for this tab. Passwords and access tokens stay out of storage.
export function readRefreshToken() {
  try { const token = globalThis.sessionStorage?.getItem(key); return typeof token === 'string' && token.length > 0 && token.length <= 512 ? token : null; } catch { return null; }
}
export function saveRefreshToken(token) {
  try { globalThis.sessionStorage?.setItem(key, token); } catch { /* Storage may be disabled; in-memory sign-in still works. */ }
}
export function removeRefreshToken() {
  try { globalThis.sessionStorage?.removeItem(key); } catch { /* Storage may be disabled. */ }
}

const apiBase = (import.meta.env?.VITE_API_BASE_URL || '/api').replace(/\/$/, '');
export class ApiError extends Error {
  constructor(message, status) { super(message); this.name = 'ApiError'; this.status = status; }
}
export async function authRequest(action, body, accessToken) {
  let response;
  try {
    response = await fetch(apiBase + '/auth/' + action, {
      method: 'POST', headers: { 'Content-Type': 'application/json', ...(accessToken ? { Authorization: 'Bearer ' + accessToken } : {}) },
      body: body === undefined ? undefined : JSON.stringify(body), cache: 'no-store', signal: AbortSignal.timeout(15000)
    });
  } catch { throw new ApiError('Cannot reach the server. Check your connection and try again.', 0); }
  if (!response.ok) {
    const messages = { 400: 'Check your username and password and try again.', 401: action === 'login' ? 'The username or password is incorrect, or the account is unavailable.' : 'Your session has expired. Please sign in again.', 403: 'Your account does not have access.', 429: 'Too many attempts. Please wait a minute and try again.' };
    throw new ApiError(messages[response.status] || 'The server could not complete your request. Please try again.', response.status);
  }
  if (response.status === 204 || action === 'logout') return null;
  const result = await response.json();
  if (!result.accessToken || !result.refreshToken || !result.user || !Number.isFinite(Date.parse(result.expiresOn))) throw new ApiError('The server returned an invalid sign-in response.', 502);
  return result;
}

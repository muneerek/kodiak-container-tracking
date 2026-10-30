import { ApiError } from './auth-api.js';
export function createApiClient(getToken, expire) {
  return async function request(path, { method = 'GET', body, version, signal, responseType } = {}) {
    let response;
    try {
      response = await fetch((import.meta.env?.VITE_API_BASE_URL || '/api').replace(/\/$/, '') + path, {
        method, signal: signal || AbortSignal.timeout(20000), cache: 'no-store',
        headers: { Authorization: 'Bearer ' + getToken(), ...(body === undefined || body instanceof FormData ? {} : { 'Content-Type': 'application/json' }), ...(version ? { 'If-Match': JSON.stringify(String(version)) } : {}) },
        body: body === undefined ? undefined : body instanceof FormData ? body : JSON.stringify(body)
      });
    } catch (error) { if (error.name === 'AbortError') throw error; throw new ApiError('Cannot reach the server. Please try again.', 0); }
    if (!response.ok) {
      let problem = {}; try { problem = await response.json(); } catch { /* Non-JSON server response. */ }
      if (response.status === 401) expire();
      const defaults = { 401: 'Your session has expired. Please sign in again.', 403: 'You do not have permission for this action.', 404: 'This record no longer exists. Refresh the list.', 412: 'This record was changed by another user. Close this window and reopen the record before saving.', 428: 'Reload this record before saving.' };
      const details = Object.values(problem.errors || {}).flat().join(' ');
      throw new ApiError(defaults[response.status] || (response.status < 500 && (details || problem.detail)) || 'The server could not complete your request.', response.status);
    }
    return response.status === 204 ? null : responseType === 'blob' ? response.blob() : response.json();
  };
}

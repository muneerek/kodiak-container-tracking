import { ApiError } from './auth-api.js';
const apiBase = (import.meta.env?.VITE_API_BASE_URL || '/api').replace(/\/$/, '');
export async function publicRequest(path, { body, signal } = {}) {
  let response;
  try {
    response = await fetch(apiBase + '/registration' + path, { method: body ? 'POST' : 'GET', headers: body ? { 'Content-Type': 'application/json' } : {}, body: body ? JSON.stringify(body) : undefined, signal: signal || AbortSignal.timeout(20000), cache: 'no-store' });
  } catch (error) { if (error.name === 'AbortError') throw error; throw new ApiError('Cannot reach the server. Please try again.', 0); }
  if (!response.ok) {
    let problem = {}; try { problem = await response.json(); } catch { /* Non-JSON gateway errors. */ }
    const details = Object.values(problem.errors || {}).flat().join(' ');
    throw new ApiError(response.status === 429 ? 'Too many requests. Wait a minute and try again.' : response.status < 500 && (details || problem.detail) || 'Registration is currently unavailable. Please try again later.', response.status);
  }
  return response.json();
}
export const registrationApi = {
  register: body => publicRequest('/shippers', { body }),
  options: (kind, search = '', page = 1, signal) => publicRequest('/options/' + kind + '?page=' + page + '&pageSize=100&search=' + encodeURIComponent(search), { signal }),
  async allOptions(kind) {
    const items = []; let page = 1; let total;
    do { const result = await this.options(kind, '', page++); items.push(...result.items); total = result.totalCount; } while (items.length < total);
    return items;
  }
};

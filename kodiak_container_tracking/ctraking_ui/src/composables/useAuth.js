import { readonly, ref } from 'vue';
import { authRequest } from '../services/auth-api.js';
import { readRefreshToken, saveRefreshToken, removeRefreshToken } from '../services/session-storage.js';
import { createApiClient } from '../services/http-client.js';
// Restore a tab session through the API; never trust cached user details.
const session = ref(null);
const notice = ref('');
const restoring = ref(true);
let restoration;
let refreshTimer;
let refreshing;
function clearSession(message = '', keepRefreshToken = false) { clearTimeout(refreshTimer); session.value = null; notice.value = message; if (!keepRefreshToken) removeRefreshToken(); }
function acceptSession(result) {
  saveRefreshToken(result.refreshToken);
  session.value = result;
  clearTimeout(refreshTimer);
  refreshTimer = setTimeout(() => refresh().catch(() => {}), Math.max(1000, Date.parse(result.expiresOn) - Date.now() - 60000));
}
async function refresh() {
  if (!session.value) return;
  if (!refreshing) {
    refreshing = authRequest('refresh', { refreshToken: session.value.refreshToken })
      .then(result => { if (session.value) acceptSession(result); })
      .catch(error => { clearSession(error.message, ![400, 401, 403].includes(error.status)); throw error; })
      .finally(() => { refreshing = null; });
  }
  return refreshing;
}
async function restoreSession() {
  if (!restoration) restoration = (async () => {
    const token = readRefreshToken();
    try { if (token) acceptSession(await authRequest('refresh', { refreshToken: token })); }
    catch (error) { clearSession(error.message, ![400, 401, 403].includes(error.status)); }
    finally { restoring.value = false; }
  })();
  return restoration;
}
export function useAuth() {
  return {
    session: readonly(session), notice: readonly(notice), restoring: readonly(restoring), restoreSession,
    request: createApiClient(() => session.value?.accessToken || '', () => clearSession('Your session has expired. Please sign in again.')),
    updateCompanyDetails(details) {
      if(session.value && details.uniqueId === session.value.user.linkedUniqueId)
        session.value = {...session.value,user:{...session.value.user,details}};
    },
    async signIn(userName, password) { notice.value = ''; acceptSession(await authRequest('login', { userName: userName.trim(), password })); },
    async signOut() {
      clearTimeout(refreshTimer);
      try {
        if (refreshing) await refreshing;
        if (session.value) await authRequest('logout', undefined, session.value.accessToken);
        clearSession();
      } catch (error) {
        if (error.status === 401) clearSession();
        else if (session.value) { acceptSession(session.value); throw error; }
      }
    }
  };
}

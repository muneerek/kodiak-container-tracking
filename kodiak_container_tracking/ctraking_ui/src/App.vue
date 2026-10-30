<script setup>
import { ref, watch } from 'vue';
import ThemeToggle from './components/ThemeToggle.vue';
import LoginView from './views/LoginView.vue';
import ShipperRegistrationView from './views/ShipperRegistrationView.vue';
import WorkspaceView from './views/WorkspaceView.vue';
import { useAuth } from './composables/useAuth.js';
const { session, restoring, restoreSession } = useAuth();
restoreSession();
const authPage = ref('login');
const registered = ref(null);
function registrationComplete(result) { registered.value = result; authPage.value = 'login'; }
const theme = ref('dark');
try { theme.value = localStorage.getItem('kodiak-theme') === 'light' ? 'light' : 'dark'; } catch { /* Storage may be disabled. */ }
watch(theme, value => { document.documentElement.dataset.theme = value; try { localStorage.setItem('kodiak-theme', value); } catch { /* Optional preference. */ } }, { immediate: true });
watch([session, authPage], ([value, currentPage]) => { document.title = value ? 'Kodiak | Workspace' : currentPage === 'register' ? 'Kodiak | Shipper registration' : 'Kodiak | Sign in'; }, { immediate: true });
</script>
<template>
  <div v-if="restoring" class="session-restoring" role="status" aria-live="polite">Restoring your session...</div>
  <template v-else-if="!session">
    <LoginView :registration="registered" @register="authPage = 'register'"><template #theme><ThemeToggle v-model="theme" /></template></LoginView>
    <ShipperRegistrationView v-if="authPage === 'register'" @back="authPage = 'login'" @registered="registrationComplete" />
  </template>
  <WorkspaceView v-else><template #theme><ThemeToggle v-model="theme" /></template></WorkspaceView>
</template>

<script setup>
import { reactive, ref, watch } from 'vue';
import { Boxes, Truck, UserRound, LockKeyhole, Eye, EyeOff, ArrowRight, ShieldCheck, LoaderCircle } from 'lucide-vue-next';
import { useAuth } from '../composables/useAuth.js';
const props = defineProps({ registration: Object });
defineEmits(['register']);
const { signIn, notice } = useAuth();
const form = reactive({ userName: props.registration?.userName || '', password: '' });
watch(() => props.registration, result => { if (result) { form.userName = result.userName; form.password = ''; error.value = ''; } });
const busy = ref(false);
const visible = ref(false);
const error = ref('');
async function submit() {
  if (busy.value) return;
  busy.value = true; error.value = '';
  try { await signIn(form.userName, form.password); form.password = ''; }
  catch (failure) { error.value = failure.message; }
  finally { busy.value = false; }
}
</script>
<template>
  <main class="login-page">
    <section class="login-story" aria-label="Kodiak container tracking">
      <div class="brand login-brand"><span class="brand-mark"><Boxes :size="25" /></span><span>kodiak<span class="brand-dot">.</span></span></div>
      <div class="story-copy"><div class="eyebrow light"><span class="live-dot"></span> CONNECTED CONTAINER OPERATIONS</div><h1>Every container.<br>Every milestone.<br><span>One clear view.</span></h1><p>Bring your shippers, consignees, and global shipments together. A simpler way to keep business moving.</p></div>
      <div class="story-bottom"><span><ShieldCheck :size="16" /> Clear roles. Connected teams.</span><span>KODIAK LOGISTICS</span></div>
      <div class="container-art" aria-hidden="true"><div></div><div></div><div></div></div>
    </section>
    <section class="login-panel" aria-labelledby="login-title">
      <div class="login-top"><slot name="theme" /></div>
      <div class="login-content">
        <div class="welcome-icon"><Truck :size="26" aria-hidden="true" /></div>
        <h2 id="login-title">Kodiak Tracking</h2><p class="muted">Sign in to your account</p>
        <p v-if="registration" class="employee-notice registration-success" role="status">Registration successful. Your shipper code is {{ registration.uniqueCode }}. Sign in with your new credentials.</p>
        <form class="login-form" @submit.prevent="submit" :aria-busy="busy">
          <label for="username">Username<div class="input-icon"><UserRound :size="17" aria-hidden="true" /><input id="username" v-model="form.userName" autocomplete="username" placeholder="Enter your username" maxlength="256" required :disabled="busy" spellcheck="false" autocapitalize="none" /></div></label>
          <label for="password">Password<div class="input-icon password-field"><LockKeyhole :size="17" aria-hidden="true" /><input id="password" v-model="form.password" :type="visible ? 'text' : 'password'" autocomplete="current-password" placeholder="Enter your password" maxlength="128" required :disabled="busy" /><button class="password-toggle" type="button" :aria-label="visible ? 'Hide password' : 'Show password'" :aria-pressed="visible" @click="visible = !visible"><EyeOff v-if="visible" :size="17" /><Eye v-else :size="17" /></button></div></label>
          <p v-if="error || notice" class="form-error" role="alert">{{ error || notice }}</p>
          <button class="btn primary login-submit" type="submit" :disabled="busy || !form.userName.trim() || !form.password"><LoaderCircle v-if="busy" class="spin" :size="17" />{{ busy ? 'Signing in...' : 'Sign In' }}<ArrowRight v-if="!busy" :size="17" /></button>
        </form>
        <div class="register-line">New to Kodiak? <button type="button" @click="$emit('register')" :disabled="busy">Register as a shipper <ArrowRight :size="14" /></button></div>
        <p class="account-help"><ShieldCheck :size="15" /> Access is managed by your administrator.</p>
      </div>
      <footer class="login-footer"><span>&copy; {{ new Date().getFullYear() }} Eknixis. All rights reserved.</span><span>Built to keep you moving.</span></footer>
    </section>
  </main>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue';
import { X, LoaderCircle, Eye, EyeOff } from 'lucide-vue-next';
import ConsigneeSelect from '../components/ConsigneeSelect.vue';
import { registrationApi } from '../services/registration-api.js';
const emit = defineEmits(['back', 'registered']);
const form = reactive({ companyName: '', contactPerson: '', emailId: '', phoneNumber: '', address: '', consigneeId: '', countryId: '', businessCategoryId: '', userName: '', password: '', confirmPassword: '' });
const busy = ref(false); const error = ref(''); const optionsError = ref(''); const optionsLoading = ref(true); const visible = ref(false);
const countries = ref([]); const categories = ref([]);
let disposed = false;
const canSubmit = computed(() => !busy.value);
const dialog = ref(null);
let previousOverflow;
function close() { if (!busy.value) emit('back'); }
async function loadOptions() {
  optionsLoading.value = true; optionsError.value = '';
  try {
    const [countryItems, categoryItems] = await Promise.all([registrationApi.allOptions('countries'), registrationApi.allOptions('businesscategories')]);
    if (!disposed) { countries.value = countryItems; categories.value = categoryItems; }
  } catch (failure) { if (!disposed) optionsError.value = failure.message; }
  finally { if (!disposed) optionsLoading.value = false; }
}
async function submit() {
  if (!canSubmit.value) return;
  error.value = '';
  if (form.password !== form.confirmPassword) { error.value = 'Password and confirmation must match.'; return; }
  busy.value = true;
  try {
    const result = await registrationApi.register({ ...form, companyName: form.companyName.trim(), contactPerson: form.contactPerson.trim(), emailId: form.emailId.trim(), phoneNumber: form.phoneNumber.trim(), address: form.address.trim(), userName: form.userName.trim(), consigneeId: Number(form.consigneeId) || null, countryId: Number(form.countryId) || null, businessCategoryId: Number(form.businessCategoryId) || null });
    form.password = ''; form.confirmPassword = ''; emit('registered', result);
  } catch (failure) { error.value = failure.message; } finally { busy.value = false; }
}
onMounted(() => { previousOverflow = document.body.style.overflow; document.body.style.overflow = 'hidden'; dialog.value.showModal(); loadOptions(); });
onBeforeUnmount(() => { dialog.value?.close(); document.body.style.overflow = previousOverflow; disposed = true; form.password = ''; form.confirmPassword = ''; });
</script>
<template>
  <Teleport to="body">
    <dialog ref="dialog" class="employee-dialog registration-dialog" aria-labelledby="registration-title" aria-describedby="registration-description" @cancel.prevent="close">
      <div class="modal-header"><div><p class="eyebrow">KODIAK TRACKING</p><h2 id="registration-title">Register as a shipper</h2></div><button type="button" class="icon-btn" aria-label="Close registration" @click="close" :disabled="busy"><X :size="20" /></button></div>
      <form class="registration-form" @submit.prevent="submit" :aria-busy="busy">
        <div class="modal-body">
          <p id="registration-description" class="muted registration-dialog-intro">Connect your company to Kodiak Container Tracking.</p>
          <div v-if="optionsError" class="form-error" role="alert">{{ optionsError }}<button type="button" class="text-btn" @click="loadOptions" :disabled="busy || optionsLoading">Retry</button></div>
      <fieldset :disabled="busy"><legend>Company details</legend><div class="registration-grid">
        <label>Company name *<input v-model="form.companyName" required maxlength="300" autocomplete="organization" /></label><label>Contact person *<input v-model="form.contactPerson" required maxlength="300" autocomplete="name" /></label>
        <label>Email address *<input v-model="form.emailId" type="email" required maxlength="150" autocomplete="email" /></label><label>Phone number *<input v-model="form.phoneNumber" type="tel" required maxlength="30" autocomplete="tel" /></label>
        <label class="registration-wide">Address *<textarea v-model="form.address" required maxlength="2000" rows="3" autocomplete="street-address"></textarea></label>
        <label>Country<select v-model="form.countryId" :disabled="optionsLoading"><option value="">Select country (optional)</option><option v-for="item in countries" :key="item.id" :value="item.id">{{ item.name }}</option></select></label>
        <label>Business category<select v-model="form.businessCategoryId" :disabled="optionsLoading"><option value="">Select category (optional)</option><option v-for="item in categories" :key="item.id" :value="item.id">{{ item.name }}</option></select></label>
        <ConsigneeSelect v-model="form.consigneeId" :disabled="busy" />
      </div></fieldset>
      <fieldset :disabled="busy"><legend>Sign-in details</legend><div class="registration-grid"><label class="registration-wide">Username *<input v-model="form.userName" required maxlength="256" autocomplete="username" autocapitalize="none" spellcheck="false" /></label><label>Password *<div class="input-icon password-field"><input v-model="form.password" :type="visible ? 'text' : 'password'" required minlength="12" maxlength="128" autocomplete="new-password" /><button type="button" class="password-toggle" :aria-label="visible ? 'Hide passwords' : 'Show passwords'" :aria-pressed="visible" @click="visible = !visible"><EyeOff v-if="visible" :size="17" /><Eye v-else :size="17" /></button></div><small>12 to 128 characters</small></label><label>Confirm password *<input v-model="form.confirmPassword" :type="visible ? 'text' : 'password'" required minlength="12" maxlength="128" autocomplete="new-password" /></label></div></fieldset>

          <p v-if="error" class="form-error" role="alert">{{ error }}</p>
        </div>
        <div class="modal-footer"><button type="button" class="btn secondary" @click="close" :disabled="busy">Cancel</button><button class="btn primary" type="submit" :disabled="!canSubmit"><LoaderCircle v-if="busy" class="spin" :size="16" />{{ busy ? 'Creating your account...' : 'Create shipper account' }}</button></div>
      </form>
    </dialog>
  </Teleport>
</template>

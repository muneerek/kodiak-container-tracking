<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { Plus, RefreshCw, Eye, Pencil, Trash2, X, Building2, ChevronLeft, ChevronRight, LoaderCircle, Search } from 'lucide-vue-next';
import CompanyDirectoryCard from '../components/CompanyDirectoryCard.vue';
import { useAuth } from '../composables/useAuth.js';
import { consigneeApi } from '../services/consignee-api.js';
const api = consigneeApi(useAuth().request);
const search = ref(''); let searchTimer; let loadId = 0;
const records = ref([]); const countries = ref([]); const total = ref(0); const page = ref(1); const pageSize = ref(25);
const loading = ref(false); const opening = ref(false); const saving = ref(false); const error = ref(''); const formError = ref(''); const countryError = ref(''); const success = ref('');
const dialog = ref(null); const mode = ref('create'); const selected = ref(null); const form = ref({}); let disposed = false;
const pages = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)));
const dialogTitle = computed(() => ({ create: 'Add consignee', view: 'Consignee details', edit: 'Edit consignee', delete: 'Delete consignee' })[mode.value]);
function countryName(id) { return countries.value.find(item => item.id === id)?.name || (id ? '#' + id : '-'); }
async function load() {
  const currentLoad = ++loadId; loading.value = true; error.value = '';
  try { const result = await api.list(page.value, pageSize.value, search.value.trim()); if (!disposed && currentLoad === loadId) { records.value = result.items; total.value = result.totalCount; } }
  catch (failure) { if (!disposed && currentLoad === loadId) error.value = failure.message; } finally { if (!disposed && currentLoad === loadId) loading.value = false; }
}
async function loadCountries() {
  countryError.value = '';
  try { const items = await api.countries(); if (!disposed) countries.value = items; }
  catch { if (!disposed) countryError.value = 'Could not load countries. Retry before selecting or changing a country.'; }
}
async function open(action, row) {
  if (opening.value) return;
  opening.value = true; formError.value = ''; error.value = '';
  try {
    const item = row ? await api.get(row.uniqueId) : null; if (disposed) return;
    selected.value = item; mode.value = action;
    form.value = { companyName: item?.companyName || '', lastName: item?.lastName || '', emailId: item?.emailId || '', phoneNumber: item?.phoneNumber || '', address: item?.address || '', countryId: item?.countryId || '', status: item?.status || 1 };
    await nextTick(); if (!disposed) dialog.value.showModal();
  } catch (failure) { if (!disposed) error.value = failure.message; } finally { if (!disposed) opening.value = false; }
}
function close() { if (!saving.value) dialog.value.close(); }
async function save() {
  if (saving.value || mode.value === 'view') return;
  saving.value = true; formError.value = ''; success.value = '';
  try {
    if (mode.value === 'delete') await api.remove(selected.value.uniqueId, selected.value.version);
    else {
      const payload = { companyName: form.value.companyName.trim(), lastName: form.value.lastName.trim() || null, emailId: form.value.emailId.trim(), phoneNumber: form.value.phoneNumber.trim() || null, address: form.value.address.trim(), countryId: Number(form.value.countryId) || null, status: form.value.status, logo: selected.value?.logo || null };
      if (mode.value === 'create') await api.create(payload); else await api.update(selected.value.uniqueId, selected.value.version, payload);
    }
    if (!disposed) { dialog.value.close(); success.value = mode.value === 'delete' ? 'Consignee marked as blacklisted.' : mode.value === 'create' ? 'Consignee created successfully.' : 'Consignee updated successfully.'; await load(); }
  } catch (failure) { if (!disposed) formError.value = failure.message; } finally { if (!disposed) saving.value = false; }
}
async function changePage(value) { page.value = value; await load(); }
watch(search, () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => changePage(1), 300); });
onMounted(() => { load(); loadCountries(); });
onBeforeUnmount(() => { disposed = true; clearTimeout(searchTimer); dialog.value?.close(); });
</script>
<template>
  <section aria-labelledby="consignees-title"><div class="page-heading"><div><p class="eyebrow">YOUR CONNECTED NETWORK</p><h1 id="consignees-title">Consignees</h1><p>Manage consignee companies, contact details, and availability.</p></div><button class="btn primary" @click="open('create')" :disabled="opening"><Plus :size="17" />Add consignee</button></div>
    <p v-if="success" class="employee-notice" role="status">{{ success }}</p><p v-if="error" class="form-error" role="alert">{{ error }}</p><div v-if="countryError" class="form-error" role="alert">{{ countryError }}<button class="text-btn" @click="loadCountries">Retry</button></div>
    <section class="company-directory" :aria-busy="loading" aria-label="consignees directory">
      <div class="directory-toolbar"><label class="directory-search"><Search :size="17" /><span class="sr-only">Search consignees</span><input v-model="search" type="search" placeholder="Search by company, code, email or address" /><button v-if="search" class="icon-btn" aria-label="Clear search" @click="search = ''"><X :size="15" /></button></label><span class="directory-total" role="status">{{ total }} {{ total === 1 ? 'consignee' : 'consignees' }}</span><button class="btn secondary small-btn" @click="load" :disabled="loading"><RefreshCw :size="14" />Refresh</button></div>
      <div v-if="loading" class="employee-empty" role="status"><LoaderCircle class="spin" :size="24" />Loading consignees...</div>
      <div v-else-if="!records.length" class="employee-empty"><Building2 :size="35" /><h3>{{ error ? 'Unable to load consignees' : search ? 'No matching consignees' : 'No consignees yet' }}</h3><p>{{ error ? 'Use Refresh to try again.' : search ? 'Try another company name, code, email or address.' : 'Add a consignee to grow your connected network.' }}</p><button v-if="search" class="text-btn" @click="search = ''">Clear search</button></div>
      <div v-else class="partner-grid directory-grid"><CompanyDirectoryCard resource="consignees" v-for="item in records" :key="item.uniqueId" :item="item" :country="countryName(item.countryId)"  :busy="opening" @view="open('view', item)" @edit="open('edit', item)" @delete="open('delete', item)" /></div>
      <div class="employee-pagination directory-pagination"><label>Cards per page<select v-model="pageSize" @change="changePage(1)" :disabled="loading"><option :value="10">10</option><option :value="25">25</option><option :value="50">50</option></select></label><span>Page {{ page }} of {{ pages }}</span><div><button class="icon-btn" aria-label="Previous page" :disabled="page <= 1 || loading" @click="changePage(page - 1)"><ChevronLeft :size="18" /></button><button class="icon-btn" aria-label="Next page" :disabled="page >= pages || loading" @click="changePage(page + 1)"><ChevronRight :size="18" /></button></div></div>
    </section>
    <dialog ref="dialog" class="employee-dialog" aria-labelledby="consignee-dialog-title" @cancel="saving && $event.preventDefault()"><div class="modal-header"><div><p class="eyebrow">CONSIGNEE DIRECTORY</p><h2 id="consignee-dialog-title">{{ dialogTitle }}</h2></div><button type="button" class="icon-btn" @click="close" :disabled="saving" aria-label="Close dialog"><X :size="20" /></button></div><form @submit.prevent="save"><div class="modal-body">
      <template v-if="mode === 'delete'"><p class="employee-delete-copy">Delete <strong>{{ selected?.companyName }}</strong> ({{ selected?.uniqueCode }})?</p><p class="muted employee-delete-copy">This marks the consignee as Blacklisted. It is removed from new shipper selections, and linked consignee accounts cannot sign in. Existing shipper links and audit history are retained. Use Edit to restore Active status later.</p></template>
      <template v-else><p v-if="selected" class="employee-record-code">{{ selected.uniqueCode }} &middot; Consignee ID {{ selected.uniqueId }}</p><fieldset class="employee-fields" :disabled="mode === 'view' || saving"><legend class="sr-only">Consignee details</legend><label>Company name *<input v-model="form.companyName" required maxlength="300" autocomplete="organization" /></label><label>Contact person<input v-model="form.lastName" maxlength="300" autocomplete="name" /></label><label>Email address *<input v-model="form.emailId" type="email" required maxlength="150" autocomplete="email" /></label><label>Phone number<input v-model="form.phoneNumber" type="tel" maxlength="30" autocomplete="tel" /></label><label>Country<select v-model="form.countryId" :disabled="!!countryError"><option value="">Not selected</option><option v-if="form.countryId && !countries.some(c => c.id === Number(form.countryId))" :value="form.countryId">Current country #{{ form.countryId }}</option><option v-for="country in countries" :key="country.id" :value="country.id" :disabled="!country.isActive">{{ country.name }}{{ country.isActive ? '' : ' (inactive)' }}</option></select></label><label>Status<select v-model="form.status"><option :value="1">Active</option><option :value="2">Blacklisted</option></select></label><label class="registration-wide">Address *<textarea v-model="form.address" required maxlength="2000" rows="3" autocomplete="street-address"></textarea></label></fieldset></template>
      <p v-if="formError" class="form-error" role="alert">{{ formError }}</p></div><div class="modal-footer"><button class="btn secondary" type="button" @click="close" :disabled="saving">{{ mode === 'view' ? 'Close' : 'Cancel' }}</button><button v-if="mode !== 'view'" class="btn" :class="mode === 'delete' ? 'danger' : 'primary'" type="submit" :disabled="saving"><LoaderCircle v-if="saving" class="spin" :size="15" />{{ saving ? 'Saving...' : mode === 'delete' ? 'Delete consignee' : 'Save consignee' }}</button></div></form></dialog>
  </section>
</template>

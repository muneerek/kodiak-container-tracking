<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { Plus, RefreshCw, Eye, Pencil, Trash2, X, Building2, ChevronLeft, ChevronRight, LoaderCircle, Search } from 'lucide-vue-next';
import CompanyDirectoryCard from '../components/CompanyDirectoryCard.vue';
import { useAuth } from '../composables/useAuth.js';
import EmployeePhotoEditor from '../components/EmployeePhotoEditor.vue';
import ConsigneeSelect from '../components/ConsigneeSelect.vue';
import { shipperApi } from '../services/shipper-api.js';
const { session, request } = useAuth();
const api = shipperApi(request);
const isAdmin = computed(() => session.value?.user.userType === 1);
const categories = ref([]); const editorKey = ref(0); const pendingLogo = ref(undefined); const cropping = ref(false);
const consigneeOption = computed(() => selected.value?.consignee ? { id: selected.value.consignee.consigneeId, code: selected.value.consignee.uniqueCode, name: selected.value.consignee.companyName } : null);
const search = ref(''); let searchTimer; let loadId = 0;
const records = ref([]); const countries = ref([]); const total = ref(0); const page = ref(1); const pageSize = ref(25);
const loading = ref(false); const opening = ref(false); const saving = ref(false); const error = ref(''); const formError = ref(''); const countryError = ref(''); const success = ref('');
const dialog = ref(null); const mode = ref('create'); const selected = ref(null); const form = ref({}); let disposed = false;
const pages = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)));
const dialogTitle = computed(() => ({ create: 'Add shipper', view: 'Shipper details', edit: 'Edit shipper', delete: 'Delete shipper' })[mode.value]);
function countryName(id) { return countries.value.find(item => item.id === id)?.name || (id ? '#' + id : '-'); }
async function load() {
  const currentLoad = ++loadId; loading.value = true; error.value = '';
  try { const result = await api.list(page.value, pageSize.value, search.value.trim()); if (!disposed && currentLoad === loadId) { records.value = result.items; total.value = result.totalCount; } }
  catch (failure) { if (!disposed && currentLoad === loadId) error.value = failure.message; } finally { if (!disposed && currentLoad === loadId) loading.value = false; }
}
async function loadCountries() {
  countryError.value = '';
  try { const [items, categoryItems] = await Promise.all([api.countries(), api.categories()]); if (!disposed) { countries.value = items; categories.value = categoryItems; } }
  catch { if (!disposed) countryError.value = 'Could not load countries/categories. Retry before changing these selections.'; }
}
async function open(action, row) {
  if (opening.value) return;
  opening.value = true; formError.value = ''; error.value = '';
  try {
    const item = row ? await api.get(row.uniqueId) : null; if (disposed) return;
    selected.value = item; mode.value = action; editorKey.value++; pendingLogo.value = undefined; cropping.value = false;
    form.value = { companyName: item?.companyName || '', lastName: item?.lastName || '', emailId: item?.emailId || '', phoneNumber: item?.phoneNumber || '', address: item?.address || '', countryId: item?.countryId || '', status: item?.status || 1, consigneeId: item?.consigneeId || '', businessCategoryId: item?.businessCategoryId || '' };
    await nextTick(); if (!disposed) dialog.value.showModal();
  } catch (failure) { if (!disposed) error.value = failure.message; } finally { if (!disposed) opening.value = false; }
}
function close() { if (!saving.value) dialog.value.close(); }
async function save() {
  if (saving.value || cropping.value || mode.value === 'view') return;
  saving.value = true; formError.value = ''; success.value = ''; const action = mode.value; let profileSaved = false;
  try {
    if (mode.value === 'delete') await api.remove(selected.value.uniqueId, selected.value.version);
    else {
      const payload = { companyName: form.value.companyName.trim(), lastName: form.value.lastName.trim() || null, emailId: form.value.emailId.trim(), phoneNumber: form.value.phoneNumber.trim() || null, address: form.value.address.trim(), countryId: Number(form.value.countryId) || null, status: isAdmin.value ? form.value.status : selected.value.status, consigneeId: Number(form.value.consigneeId) || null, businessCategoryId: Number(form.value.businessCategoryId) || null, logo: selected.value?.logo || null };
      const result = mode.value === 'create' ? await api.create(payload) : await api.update(selected.value.uniqueId, selected.value.version, payload);
      selected.value = result; profileSaved = true;
      if (pendingLogo.value !== undefined) {
        mode.value = 'edit';
        selected.value = pendingLogo.value === null ? await api.removeLogo(result.uniqueId, result.version) : await api.uploadLogo(result.uniqueId, result.version, pendingLogo.value);
        pendingLogo.value = undefined;
      }
    }
    if (!disposed) { dialog.value.close(); success.value = action === 'delete' ? 'Shipper marked as blacklisted.' : action === 'create' ? 'Shipper created successfully.' : 'Shipper updated successfully.'; await load(); }
  } catch (failure) { if (!disposed) formError.value = profileSaved && pendingLogo.value !== undefined ? 'Shipper details were saved, but the logo was not saved. ' + failure.message : failure.message; } finally { if (!disposed) saving.value = false; }
}
async function changePage(value) { page.value = value; await load(); }
watch(search, () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => changePage(1), 300); });
onMounted(() => { load(); loadCountries(); });
onBeforeUnmount(() => { disposed = true; clearTimeout(searchTimer); dialog.value?.close(); });
</script>
<template>
  <section aria-labelledby="shippers-title"><div class="page-heading"><div><p class="eyebrow">YOUR CONNECTED NETWORK</p><h1 id="shippers-title">{{ isAdmin ? 'Shippers' : 'My shipper details' }}</h1><p>Manage company details, contacts, and logos.</p></div><button v-if="isAdmin" class="btn primary" @click="open('create')" :disabled="opening"><Plus :size="17" />Add shipper</button></div>
    <p v-if="success" class="employee-notice" role="status">{{ success }}</p><p v-if="error" class="form-error" role="alert">{{ error }}</p><div v-if="countryError" class="form-error" role="alert">{{ countryError }}<button class="text-btn" @click="loadCountries">Retry</button></div>
    <section class="company-directory" :aria-busy="loading" aria-label="shippers directory">
      <div class="directory-toolbar"><label class="directory-search"><Search :size="17" /><span class="sr-only">Search shippers</span><input v-model="search" type="search" placeholder="Search by company, code, email or address" /><button v-if="search" class="icon-btn" aria-label="Clear search" @click="search = ''"><X :size="15" /></button></label><span class="directory-total" role="status">{{ total }} {{ total === 1 ? 'shipper' : 'shippers' }}</span><button class="btn secondary small-btn" @click="load" :disabled="loading"><RefreshCw :size="14" />Refresh</button></div>
      <div v-if="loading" class="employee-empty" role="status"><LoaderCircle class="spin" :size="24" />Loading shippers...</div>
      <div v-else-if="!records.length" class="employee-empty"><Building2 :size="35" /><h3>{{ error ? 'Unable to load shippers' : search ? 'No matching shippers' : 'No shippers yet' }}</h3><p>{{ error ? 'Use Refresh to try again.' : search ? 'Try another company name, code, email or address.' : 'Add a shipper to grow your connected network.' }}</p><button v-if="search" class="text-btn" @click="search = ''">Clear search</button></div>
      <div v-else class="partner-grid directory-grid"><CompanyDirectoryCard resource="shippers" v-for="item in records" :key="item.uniqueId" :item="item" :country="countryName(item.countryId)" :category="categories.find(category => category.id === item.businessCategoryId)?.name" :can-delete="isAdmin" :busy="opening" @view="open('view', item)" @edit="open('edit', item)" @delete="open('delete', item)" /></div>
      <div class="employee-pagination directory-pagination"><label>Cards per page<select v-model="pageSize" @change="changePage(1)" :disabled="loading"><option :value="10">10</option><option :value="25">25</option><option :value="50">50</option></select></label><span>Page {{ page }} of {{ pages }}</span><div><button class="icon-btn" aria-label="Previous page" :disabled="page <= 1 || loading" @click="changePage(page - 1)"><ChevronLeft :size="18" /></button><button class="icon-btn" aria-label="Next page" :disabled="page >= pages || loading" @click="changePage(page + 1)"><ChevronRight :size="18" /></button></div></div>
    </section>
    <dialog ref="dialog" class="employee-dialog" aria-labelledby="shipper-dialog-title" @cancel="saving && $event.preventDefault()"><div class="modal-header"><div><p class="eyebrow">SHIPPER DIRECTORY</p><h2 id="shipper-dialog-title">{{ dialogTitle }}</h2></div><button type="button" class="icon-btn" @click="close" :disabled="saving" aria-label="Close dialog"><X :size="20" /></button></div><form @submit.prevent="save"><div class="modal-body">
      <template v-if="mode === 'delete'"><p class="employee-delete-copy">Delete <strong>{{ selected?.companyName }}</strong> ({{ selected?.uniqueCode }})?</p><p class="muted employee-delete-copy">This marks the shipper as Blacklisted. Linked shipper accounts cannot sign in while blacklisted. Existing shipper links and audit history are retained. Use Edit to restore Active status later.</p></template>
      <template v-else><p v-if="selected" class="employee-record-code">{{ selected.uniqueCode }} &middot; Shipper ID {{ selected.uniqueId }}</p><EmployeePhotoEditor :key="editorKey + '-logo'" :employee-id="selected?.uniqueId" :path="selected?.logo" resource="shippers" label="Shipper logo" :readonly="mode === 'view' || saving" @change="pendingLogo = $event" @editing="cropping = $event" /><fieldset class="employee-fields" :disabled="mode === 'view' || saving"><legend class="sr-only">Shipper details</legend><label>Company name *<input v-model="form.companyName" required maxlength="300" autocomplete="organization" /></label><label>Contact person<input v-model="form.lastName" maxlength="300" autocomplete="name" /></label><label>Email address *<input v-model="form.emailId" type="email" required maxlength="150" autocomplete="email" /></label><label>Phone number<input v-model="form.phoneNumber" type="tel" maxlength="30" autocomplete="tel" /></label><label>Country<select v-model="form.countryId" :disabled="!!countryError"><option value="">Not selected</option><option v-if="form.countryId && !countries.some(c => c.id === Number(form.countryId))" :value="form.countryId">Current country #{{ form.countryId }}</option><option v-for="country in countries" :key="country.id" :value="country.id">{{ country.name }}</option></select></label><label>Status<select v-model="form.status" :disabled="!isAdmin"><option :value="1">Active</option><option :value="2">Blacklisted</option></select></label><label>Business category<select v-model="form.businessCategoryId" :disabled="!!countryError"><option value="">Not selected</option><option v-if="form.businessCategoryId && !categories.some(c => c.id === Number(form.businessCategoryId))" :value="form.businessCategoryId">Current category #{{ form.businessCategoryId }}</option><option v-for="category in categories" :key="category.id" :value="category.id">{{ category.name }}</option></select></label><ConsigneeSelect :key="editorKey + '-consignee'" v-model="form.consigneeId" :initial-option="consigneeOption" :disabled="saving || mode === 'view'" /><label class="registration-wide">Address *<textarea v-model="form.address" required maxlength="2000" rows="3" autocomplete="street-address"></textarea></label></fieldset></template>
      <p v-if="formError" class="form-error" role="alert">{{ formError }}</p></div><div class="modal-footer"><button class="btn secondary" type="button" @click="close" :disabled="saving">{{ mode === 'view' ? 'Close' : 'Cancel' }}</button><button v-if="mode !== 'view'" class="btn" :class="mode === 'delete' ? 'danger' : 'primary'" type="submit" :disabled="saving || cropping"><LoaderCircle v-if="saving" class="spin" :size="15" />{{ saving ? 'Saving...' : mode === 'delete' ? 'Delete shipper' : 'Save shipper' }}</button></div></form></dialog>
  </section>
</template>

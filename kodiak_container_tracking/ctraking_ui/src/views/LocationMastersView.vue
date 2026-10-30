<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue';
import { Plus, RefreshCw, Eye, Pencil, Trash2, X, MapPin, ChevronLeft, ChevronRight, LoaderCircle } from 'lucide-vue-next';
import { useAuth } from '../composables/useAuth.js';
import { locationMasterApi } from '../services/location-master-api.js';
const props = defineProps({ kind: { type: String, required: true }, embedded: Boolean });
const emit = defineEmits(['loaded']);
const canCreate = computed(() => !opening.value && !lookupError.value);
defineExpose({ create: () => open('create'), canCreate });
const config = { Countries: { title: 'Countries', singular: 'country' }, States: { title: 'States / Provinces', singular: 'state / province' }, ShippingPorts: { title: 'Shipping ports', singular: 'shipping port' } }[props.kind];
const api = locationMasterApi(props.kind, useAuth().request);
const isCountry = props.kind === 'Countries'; const isPort = props.kind === 'ShippingPorts';
const records = ref([]); const countries = ref([]); const states = ref([]); const total = ref(0); const page = ref(1); const pageSize = ref(25);
const loading = ref(false); const opening = ref(false); const saving = ref(false); const error = ref(''); const formError = ref(''); const lookupError = ref(''); const success = ref('');
const dialog = ref(null); const mode = ref('create'); const selected = ref(null); const form = ref({}); let disposed = false;
const pages = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)));
const dialogTitle = computed(() => ({ create: 'Add ', view: 'View ', edit: 'Edit ', delete: 'Delete ' })[mode.value] + config.singular);
const stateChoices = computed(() => states.value.filter(item => item.countryId === Number(form.value.countryId)));
function countryName(id) { return countries.value.find(item => item.id === id)?.name || (id ? '#' + id : '-'); }
function stateName(id) { return states.value.find(item => item.id === id)?.name || (id ? '#' + id : '-'); }
async function load() {
  loading.value = true; error.value = '';
  try { const result = await api.list(page.value, pageSize.value); if (!disposed) { records.value = result.items; total.value = result.totalCount; emit('loaded', result.totalCount); } }
  catch (failure) { if (!disposed) error.value = failure.message; } finally { if (!disposed) loading.value = false; }
}
async function loadLookups() {
  if (isCountry) return; lookupError.value = '';
  try { const [countryItems, stateItems] = await Promise.all([api.lookup('Countries'), isPort ? api.lookup('States') : Promise.resolve([])]); if (!disposed) { countries.value = countryItems; states.value = stateItems; } }
  catch { if (!disposed) lookupError.value = 'Could not load location choices. Retry before adding or editing.'; }
}
async function open(action, row) {
  if (opening.value) return; opening.value = true; formError.value = ''; error.value = '';
  try {
    const item = row ? await api.get(row.id) : null; if (disposed) return;
    const generatedCode = isPort && action === 'create' ? (await api.nextPortCode()).code : '';
    if (disposed) return;
    selected.value = item; mode.value = action;
    form.value = { code: item?.code || generatedCode, name: item?.name || '', isActive: item?.isActive ?? true, countryId: item?.countryId || '', stateId: item?.stateId || '', city: item?.city || '', timeZone: item?.timeZone || 'UTC' };
    await nextTick(); if (!disposed) dialog.value.showModal();
  } catch (failure) { if (!disposed) error.value = failure.message; } finally { if (!disposed) opening.value = false; }
}
function close() { if (!saving.value) dialog.value.close(); }
async function save() {
  if (saving.value || mode.value === 'view') return;
  saving.value = true; formError.value = ''; success.value = '';
  try {
    if (mode.value === 'delete') await api.remove(selected.value.id, selected.value.version);
    else {
      const payload = { ...(!isPort ? { code: form.value.code.trim().toUpperCase() } : {}), name: form.value.name.trim(), isActive: form.value.isActive,
        ...(!isCountry ? { countryId: Number(form.value.countryId) || null } : {}),
        ...(isPort ? { stateId: Number(form.value.stateId) || null, city: form.value.city.trim() || null } : {}) };
      if (mode.value === 'create') await api.create(payload); else await api.update(selected.value.id, selected.value.version, payload);
    }
    if (!disposed) { dialog.value.close(); success.value = mode.value === 'delete' ? 'Record deactivated.' : mode.value === 'create' ? 'Record created successfully.' : 'Record updated successfully.'; await load(); }
  } catch (failure) { if (!disposed) formError.value = failure.message; } finally { if (!disposed) saving.value = false; }
}
async function changePage(value) { page.value = value; await load(); }
onMounted(() => { load(); loadLookups(); });
onBeforeUnmount(() => { disposed = true; dialog.value?.close(); });
</script>
<template>
  <section :aria-labelledby="embedded ? undefined : 'location-master-title'" :aria-label="embedded ? config.title : undefined"><div v-if="!embedded" class="page-heading"><div><p class="eyebrow">MANAGEMENT</p><h1 id="location-master-title">{{ config.title }}</h1><p>Maintain location records and availability.</p></div><button class="btn primary" @click="open('create')" :disabled="opening || !!lookupError"><Plus :size="17" />Add {{ config.singular }}</button></div>
    <p v-if="success" class="employee-notice" role="status">{{ success }}</p><p v-if="error" class="form-error" role="alert">{{ error }}</p><div v-if="lookupError" class="form-error" role="alert">{{ lookupError }}<button class="text-btn" @click="loadLookups">Retry</button></div>
    <section class="panel employee-panel" :aria-busy="loading"><div class="panel-heading"><div v-if="!embedded"><h2>{{ config.title }} directory <span class="count-pill">{{ total }}</span></h2><p>Inactive records remain available for historical references.</p></div><span v-else class="location-record-count">{{ total }} {{ config.title.toLowerCase() }}</span><button class="btn secondary small-btn" @click="load" :disabled="loading"><RefreshCw :size="14" />Refresh</button></div>
      <div v-if="loading" class="employee-empty" role="status"><LoaderCircle class="spin" :size="24" />Loading records...</div><div v-else-if="!records.length" class="employee-empty"><MapPin :size="35" /><h3>{{ error ? 'Unable to load records' : 'No records yet' }}</h3><p>{{ error ? 'Use Refresh to try again.' : 'Add your first record to get started.' }}</p></div>
      <div v-else class="employee-table-wrap"><table class="employee-table"><caption class="sr-only">{{ config.title }} directory</caption><thead><tr><th scope="col">{{ isPort ? 'Port name' : isCountry ? 'Country name' : 'State / region' }}</th><th scope="col">Code</th><th v-if="!isCountry" scope="col">Country</th><th v-if="isPort" scope="col">State / Province</th><th v-if="isPort && !embedded" scope="col">City</th><th scope="col">Status</th><th scope="col">Actions</th></tr></thead><tbody><tr v-for="item in records" :key="item.id"><td><div class="location-name-cell"><MapPin :size="18"/><div><button class="employee-name" @click="open('view',item)" :disabled="opening">{{ item.name }}</button><small v-if="isPort && embedded && item.city">{{ item.city }}</small></div></div></td><td><span class="location-code">{{ item.code }}</span></td><td v-if="!isCountry">{{ countryName(item.countryId) }}</td><td v-if="isPort">{{ stateName(item.stateId) }}</td><td v-if="isPort && !embedded">{{ item.city || '-' }}</td><td><span class="badge" :class="item.isActive ? 'green' : 'gray'">{{ item.isActive ? 'Active' : 'Inactive' }}</span></td><td><div class="employee-row-actions"><button class="icon-btn" @click="open('view',item)" :disabled="opening" :aria-label="'View ' + item.name" title="View"><Eye :size="16" /></button><button class="icon-btn" @click="open('edit',item)" :disabled="opening || !!lookupError" :aria-label="'Edit ' + item.name" title="Edit"><Pencil :size="16" /></button><button class="icon-btn delete-action" @click="open('delete',item)" :disabled="opening || !item.isActive" :aria-label="'Delete ' + item.name" title="Delete (deactivate)"><Trash2 :size="16" /></button></div></td></tr></tbody></table></div>
      <div class="employee-pagination"><label>Rows per page<select v-model="pageSize" @change="changePage(1)" :disabled="loading"><option :value="10">10</option><option :value="25">25</option><option :value="50">50</option></select></label><span>Page {{ page }} of {{ pages }}</span><div><button class="icon-btn" aria-label="Previous page" :disabled="page <= 1 || loading" @click="changePage(page - 1)"><ChevronLeft :size="18" /></button><button class="icon-btn" aria-label="Next page" :disabled="page >= pages || loading" @click="changePage(page + 1)"><ChevronRight :size="18" /></button></div></div>
    </section>
    <dialog ref="dialog" class="employee-dialog" aria-labelledby="location-dialog-title" @cancel="saving && $event.preventDefault()"><div class="modal-header"><div><p class="eyebrow">LOCATION MASTER</p><h2 id="location-dialog-title">{{ dialogTitle }}</h2></div><button type="button" class="icon-btn" @click="close" :disabled="saving" aria-label="Close dialog"><X :size="20" /></button></div><form @submit.prevent="save"><div class="modal-body">
      <template v-if="mode === 'delete'"><p class="employee-delete-copy">Delete <strong>{{ selected?.name }}</strong> ({{ selected?.code }})?</p><p class="muted employee-delete-copy">This deactivates the record and retains existing references and audit history. Use Edit to reactivate it. Related records are not deleted or deactivated automatically.</p></template>
      <fieldset v-else class="employee-fields" :disabled="mode === 'view' || saving"><legend class="sr-only">Location details</legend><label v-if="isPort">Code<input :value="form.code" readonly placeholder="Generated when saved" /><small>Automatically generated. Final code is confirmed when saved.</small></label><label v-else>Code *<input v-model="form.code" required maxlength="50" pattern="[A-Za-z0-9_-]+" autocomplete="off" /><small>{{ isCountry ? 'For example IN, AE or CN.' : 'For example IN-KL or AE-DU.' }}</small></label><label>Name *<input v-model="form.name" required maxlength="300" /></label>
      <label v-if="!isCountry">Country *<select v-model="form.countryId" required :disabled="!isPort && !!selected" @change="form.stateId = ''"><option value="">Select country</option><option v-for="country in countries" :key="country.id" :value="country.id" :disabled="!country.isActive">{{ country.name }}{{ country.isActive ? '' : ' (inactive)' }}</option></select><small v-if="!isPort && selected">Country cannot be changed for an existing state.</small></label>
      <label v-if="isPort">State / Province<select v-model="form.stateId" :disabled="!form.countryId"><option value="">Not selected (optional)</option><option v-for="state in stateChoices" :key="state.id" :value="state.id" :disabled="!state.isActive">{{ state.name }}{{ state.isActive ? '' : ' (inactive)' }}</option></select></label><label v-if="isPort">City<input v-model="form.city" maxlength="150" /></label><label>Status<select v-model="form.isActive"><option :value="true">Active</option><option :value="false">Inactive</option></select></label></fieldset>
      <p v-if="formError" class="form-error" role="alert">{{ formError }}</p></div><div class="modal-footer"><button class="btn secondary" type="button" @click="close" :disabled="saving">{{ mode === 'view' ? 'Close' : 'Cancel' }}</button><button v-if="mode !== 'view'" class="btn" :class="mode === 'delete' ? 'danger' : 'primary'" type="submit" :disabled="saving"><LoaderCircle v-if="saving" class="spin" :size="15" />{{ saving ? 'Saving...' : mode === 'delete' ? 'Delete record' : 'Save record' }}</button></div></form></dialog>
  </section>
</template>

<style scoped>
.location-record-count{font-size:12px;color:var(--muted,#8199a9)}.location-name-cell{display:flex;align-items:center;gap:11px;min-width:165px}.location-name-cell>svg{color:#1ca6c6;flex-shrink:0}.location-name-cell small{display:block;font-size:11px;color:var(--muted,#8199a9);margin-top:6px}.location-code{display:inline-block;font-family:monospace;font-size:12px;border-radius:5px;background:#eaf3fa;color:#357a9e;padding:5px 9px}html[data-theme="dark"] .location-code{background:#91bad51c;color:#b8d9ed}
</style>

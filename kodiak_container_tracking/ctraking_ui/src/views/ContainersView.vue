<script setup>
import BusinessDatePicker from '../components/BusinessDatePicker.vue';
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { Plus, Boxes, Eye, Pencil, X, RefreshCw, LoaderCircle, ChevronLeft, ChevronRight, Ban, Search, ImagePlus, FilePlus2, Download, Trash2 } from 'lucide-vue-next';
import ContainerTrackingDialog from '../components/ContainerTrackingDialog.vue';
import ContainerFileViewer from '../components/ContainerFileViewer.vue';
import { trackingStatusName } from '../services/container-tracking.js';
import { useAuth } from '../composables/useAuth.js';
import { containerCsv, fetchContainerExport } from '../services/container-export.js';
import { containerApi } from '../services/container-api.js';
const props = defineProps({ createOnOpen: Boolean });
const { session, request } = useAuth(); const api = containerApi(request);
const isAdmin = computed(() => session.value?.user.userType === 1);
const canCreate = computed(() => [1, 3].includes(session.value?.user.userType));
const types = [{ code:'20STD', name:'20 ft Standard' },{ code:'40STD', name:'40 ft Standard' },{ code:'40HC', name:'40 ft High Cube' },{ code:'20REEFER', name:'20 ft Reefer' },{ code:'40REEFER', name:'40 ft Reefer' }];
const records = ref([]); const ports = ref([]); const shippers = ref([]); const total = ref(0); const page = ref(1); const pageSize = ref(25); const search = ref('');
const trackingDialog = ref(null); const viewingFile = ref(null);
const status = ref(''); const exporting = ref(false);
const optionsLoading = ref(true);
const loading = ref(false); const opening = ref(false); const saving = ref(false); const error = ref(''); const formError = ref(''); const lookupError = ref(''); const success = ref('');
const dialog = ref(null); const mode = ref('create'); const selected = ref(null); const form = ref({}); const files = ref([]); const removals = ref([]); const photoInput = ref(null); const documentInput = ref(null);
let disposed = false; let searchTimer; let loadId = 0;
const pages = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)));
const title = computed(() => ({create:'Register a container',view:'Container details',edit:'Edit container registration',cancel:'Cancel registration'})[mode.value]);
const consigneeName = computed(() => selected.value?.consigneeName || shippers.value.find(x => x.id === Number(form.value.shipperId))?.consigneeName || 'Not linked');
function typeName(code) { return types.find(x => x.code === code)?.name || code; }
function size(bytes) { return bytes < 1048576 ? Math.ceil(bytes / 1024) + ' KB' : (bytes / 1048576).toFixed(1) + ' MB'; }
async function load() {
  const currentLoad = ++loadId; loading.value = true; error.value = '';
  try { const result = await api.list(page.value, pageSize.value, search.value.trim(), status.value); if (!disposed && currentLoad === loadId) { records.value = result.items; total.value = result.totalCount; } }
  catch (failure) { if (!disposed && currentLoad === loadId) error.value = failure.message; } finally { if (!disposed && currentLoad === loadId) loading.value = false; }
}
async function exportRecords() {
  if (exporting.value) return; exporting.value = true; error.value = '';
  const filterSearch = search.value.trim(); const filterStatus = status.value;
  try {
    const items = await fetchContainerExport(api, filterSearch, filterStatus); if (disposed) return;
    const url = URL.createObjectURL(new Blob([containerCsv(items)], {type:'text/csv;charset=utf-8;'}));
    const link = document.createElement('a'); link.href = url; link.download = 'containers-' + new Date().toISOString().slice(0,10) + '.csv';
    document.body.appendChild(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 1000);
  } catch (failure) { if (!disposed) error.value = 'Could not export containers. ' + failure.message; }
  finally { if (!disposed) exporting.value = false; }
}
function filterNow() { clearTimeout(searchTimer); changePage(1); }
watch(search, () => { clearTimeout(searchTimer); searchTimer = setTimeout(filterNow, 300); });
watch(status, filterNow);
async function loadOptions() {
  lookupError.value = ''; optionsLoading.value = true;
  try { const [portItems, shipperItems] = await Promise.all([api.options('ports'), canCreate.value ? api.options('shippers') : Promise.resolve([])]); if (!disposed) { ports.value = portItems; shippers.value = shipperItems; } }
  catch { if (!disposed) lookupError.value = 'Could not load ports/shippers. Retry before registering or editing.'; }
  finally { if (!disposed) optionsLoading.value = false; }
}
async function open(action, row) {
  if (opening.value) return; opening.value = true; formError.value = ''; error.value = ''; viewingFile.value = null;
  try {
    const item = row ? await api.get(row.id) : null; if (disposed) return;
    selected.value = item; mode.value = action; files.value = []; removals.value = [];
    form.value = { shipperId:item?.shipperId || shippers.value[0]?.id || '', containerNumber:item?.containerNumber || '', containerType:item?.containerType || '40HC', cargo:item?.cargo || '', weightKg:item?.weightKg || '', packages:item?.packages || '', originPortId:item?.originPortId || '', destinationPortId:item?.destinationPortId || '', departure:item?.departure || '', expectedArrival:item?.expectedArrival || '', reference:item?.reference || '' };
    await nextTick(); if (!disposed) dialog.value.showModal();
  } catch (failure) { if (!disposed) error.value = failure.message; } finally { if (!disposed) opening.value = false; }
}
function close() { if (!saving.value) { viewingFile.value = null; dialog.value.close(); } }
function chooseFiles(event, kind) {
  const chosen = Array.from(event.target.files || []); event.target.value = ''; formError.value = '';
  const allowed = kind === 1 ? ['jpg','jpeg','png','webp'] : ['pdf','docx','xls','xlsx','txt'];
  if (chosen.some(file => !allowed.includes(file.name.split('.').pop().toLowerCase()) || file.size === 0 || file.size > 10485760)) { formError.value = 'Choose supported, non-empty files up to 10 MB each. Photos: JPEG/PNG/WebP. Documents: PDF/DOCX/XLS/XLSX/TXT.'; return; }
  files.value.push(...chosen.map(file => ({ file, kind, key: crypto.randomUUID() })));
}
function download(file) { viewingFile.value = file; }
async function trackingSaved(item) { if (selected.value?.id === item.id) { selected.value = item; form.value.expectedArrival = item.expectedArrival; form.value.containerNumber = item.containerNumber || ''; } success.value = 'Tracking update and supporting files saved.'; await load(); }
async function save() {
  if (saving.value || mode.value === 'view') return; saving.value = true; formError.value = ''; success.value = ''; let profileSaved = false;
  try {
    if (mode.value === 'cancel') selected.value = await api.cancel(selected.value.id, selected.value.version);
    else {
      const payload = {...form.value, shipperId:Number(form.value.shipperId), weightKg:Number(form.value.weightKg), packages:Number(form.value.packages), originPortId:Number(form.value.originPortId), destinationPortId:Number(form.value.destinationPortId), containerNumber:form.value.containerNumber.trim() || null, reference:form.value.reference.trim() || null};
      selected.value = mode.value === 'create' ? await api.create(payload) : await api.update(selected.value.id, selected.value.version, payload);
      profileSaved = true; mode.value = 'edit';
      while (removals.value.length) { selected.value = await api.removeFile(selected.value.id, removals.value[0], selected.value.version); removals.value.shift(); }
      while (files.value.length) { const pending = files.value[0]; selected.value = await api.upload(selected.value.id, selected.value.version, pending.file, pending.kind); files.value.shift(); }
    }
    if (!disposed) { success.value = selected.value.status === 2 ? 'Registration cancelled.' : 'Container registration saved successfully.'; mode.value = 'view'; await load(); }
  } catch (failure) { if (!disposed) formError.value = (profileSaved ? 'Registration details were saved. Remaining file changes were not completed; retry Save to continue. ' : '') + failure.message; }
  finally { if (!disposed) saving.value = false; }
}
async function changePage(value) { page.value = value; await load(); }
onMounted(() => { load(); loadOptions().then(() => { if (!disposed && props.createOnOpen && canCreate.value && !lookupError.value) open('create'); }); });
onBeforeUnmount(() => { disposed = true; clearTimeout(searchTimer); dialog.value?.close(); viewingFile.value = null; });
</script>
<template>
  <section aria-labelledby="containers-title"><div class="page-heading"><div><p class="eyebrow">CONTAINER OPERATIONS</p><h1 id="containers-title">Your containers.</h1><p>Manage registrations and follow your cargo from origin to destination.</p></div><button v-if="canCreate" class="btn primary" @click="open('create')" :disabled="opening || optionsLoading || !!lookupError"><Plus :size="17" />Register container</button></div>
    <p v-if="success" class="employee-notice" role="status">{{ success }}</p><p v-if="error" class="form-error" role="alert">{{ error }}</p><div v-if="lookupError" class="form-error" role="alert">{{ lookupError }}<button class="text-btn" @click="loadOptions">Retry</button></div>
    <form class="container-toolbar" @submit.prevent="filterNow"><label class="container-search-field"><Search :size="18" /><input v-model="search" type="search" maxlength="100" placeholder="Search container, booking, route..." aria-label="Search container, booking, route, shipper or reference" /></label><select v-model="status" aria-label="Filter containers by status"><option value="">All statuses</option><option value="1">Booked</option><option value="2">Cancelled</option></select><button class="btn secondary" type="button" @click="exportRecords" :disabled="exporting"><LoaderCircle v-if="exporting" class="spin" :size="16" /><Download v-else :size="16" />{{ exporting ? 'Exporting...' : 'Export' }}</button></form>
    <section class="panel employee-panel" :aria-busy="loading"><div class="panel-heading"><div><h2>Container register <span class="count-pill">{{ total }}</span></h2></div><span class="container-register-note">Manually entered &middot; Automatically organized</span></div>
      <div v-if="loading" class="employee-empty" role="status"><LoaderCircle class="spin" :size="24" />Loading containers...</div><div v-else-if="!records.length" class="employee-empty"><Boxes :size="35" /><h3>{{ error ? 'Unable to load containers' : 'No registrations found' }}</h3><p>{{ error ? 'Try again to reload the container register.' : 'Your container registrations will appear here.' }}</p><button v-if="error" class="btn secondary small-btn" @click="load"><RefreshCw :size="14" />Try again</button></div>
      <div v-else class="employee-table-wrap"><table class="employee-table"><caption class="sr-only">Container registrations</caption><thead><tr><th>Container / Booking</th><th>Shipper</th><th>Route</th><th>Status</th><th>Arrival</th><th>Actions</th></tr></thead><tbody><tr v-for="item in records" :key="item.id"><td><button class="employee-name" @click="open('view',item)" :disabled="opening">{{ item.containerNumber || 'Pending allocation' }}</button><small class="employee-id">{{ item.code }} &middot; {{ typeName(item.containerType) }}</small></td><td>{{ item.shipperName }}<small class="employee-id">{{ item.consigneeName || 'No consignee linked' }}</small></td><td>{{ item.originPortName }}<small class="employee-id">to {{ item.destinationPortName }}</small></td><td><span class="badge" :class="item.status === 1 ? 'blue' : 'gray'">{{ item.status === 1 ? trackingStatusName(item.trackingStatus) : 'Cancelled' }}</span><small v-if="item.isOnHold" class="employee-id tracking-hold-text">On hold</small></td><td>{{ item.expectedArrival }}</td><td><div class="employee-row-actions"><button class="icon-btn" @click="open('view',item)" :disabled="opening" :aria-label="'View ' + item.code"><Eye :size="16" /></button><button v-if="canCreate && item.status === 1" class="icon-btn" @click="open('edit',item)" :disabled="opening || !!lookupError" :aria-label="'Edit ' + item.code"><Pencil :size="16" /></button><button v-if="isAdmin && item.status === 1" type="button" class="icon-btn tracking-action" @click="trackingDialog.open(item)" :disabled="opening" :aria-label="'Update tracking for ' + item.code" title="Update tracking"><RefreshCw :size="16" /></button><button v-if="isAdmin && item.status === 1" class="icon-btn delete-action" @click="open('cancel',item)" :disabled="opening" :aria-label="'Cancel ' + item.code"><Ban :size="16" /></button></div></td></tr></tbody></table></div>
      <div class="employee-pagination"><label>Rows per page<select v-model="pageSize" @change="changePage(1)" :disabled="loading"><option :value="10">10</option><option :value="25">25</option><option :value="50">50</option></select></label><span>Page {{ page }} of {{ pages }}</span><div><button class="icon-btn" aria-label="Previous page" :disabled="page <= 1 || loading" @click="changePage(page-1)"><ChevronLeft :size="18" /></button><button class="icon-btn" aria-label="Next page" :disabled="page >= pages || loading" @click="changePage(page+1)"><ChevronRight :size="18" /></button></div></div>
    </section>
    <dialog ref="dialog" class="employee-dialog container-dialog" aria-labelledby="container-dialog-title" @cancel="saving && $event.preventDefault()"><div class="modal-header"><div><p class="eyebrow">{{ selected?.code || 'NEW CONTAINER' }}</p><h2 id="container-dialog-title">{{ title }}</h2></div><button class="icon-btn" type="button" @click="close" :disabled="saving" aria-label="Close dialog"><X :size="20" /></button></div><form @submit.prevent="save"><div class="modal-body">
      <template v-if="mode === 'cancel'"><p class="employee-delete-copy">Cancel <strong>{{ selected?.code }}</strong>?</p><p class="muted employee-delete-copy">The registration and its files are retained for history. Cancelled registrations cannot be edited.</p></template>
      <template v-else><p v-if="mode === 'create'" class="container-help">Submit a container registration. Leave the physical container number blank if it will be allocated later.</p><p v-if="mode === 'create' && (!shippers.length || ports.length < 2)" class="form-error">An active shipper and two active shipping ports are required. Ask an administrator to create the missing master records.</p>
        <section v-if="selected && mode === 'view'" class="container-tracking-summary"><div class="tracking-section-heading"><div><h3>Shipment tracking</h3><p>{{ trackingStatusName(selected.trackingStatus) }}{{ selected.isOnHold ? ' - On hold' : '' }}</p></div><button v-if="isAdmin && selected.status === 1" type="button" class="btn primary small-btn" @click="trackingDialog.open(selected)"><RefreshCw :size="15" />Update tracking</button></div>
          <ol v-if="selected.trackingEvents?.length" class="shipment-timeline"><li v-for="entry in selected.trackingEvents" :key="entry.id"><div class="shipment-event-heading"><strong>{{ trackingStatusName(entry.status) }}<span v-if="entry.isOnHold" class="badge amber">On hold</span></strong><time :datetime="entry.eventAt">{{ new Date(entry.eventAt).toLocaleString() }}</time></div><p>{{ entry.location }}<span v-if="entry.vesselName"> &middot; {{ entry.vesselName }}</span></p><p v-if="entry.note" class="shipment-event-note">{{ entry.note }}</p><small>Expected arrival {{ entry.expectedArrival }} &middot; Recorded {{ new Date(entry.recordedAt).toLocaleString() }}</small><div v-if="entry.files.length" class="shipment-event-files"><button v-for="file in entry.files" :key="file.id" type="button" @click="download(file)"><Eye :size="14" />{{ file.fileName }}</button></div></li></ol><p v-else class="container-help muted">No tracking updates recorded yet.</p>
        </section>
        <fieldset class="employee-fields" :disabled="mode === 'view' || saving"><legend class="container-section-title">01 &middot; Container &amp; cargo</legend><label>Shipper *<select v-model="form.shipperId" required :disabled="!!selected || !isAdmin"><option value="">Select shipper</option><option v-if="selected && !shippers.some(x => x.id === selected.shipperId)" :value="selected.shipperId">{{ selected.shipperName }}</option><option v-for="item in shippers" :key="item.id" :value="item.id">{{ item.name }}</option></select></label><label>Consignee<input :value="consigneeName" disabled /></label><label>Container number (optional)<input v-model="form.containerNumber" maxlength="24" placeholder="e.g. MSCU 782341-6" /></label><label>Container type *<select v-model="form.containerType" required><option v-for="item in types" :key="item.code" :value="item.code">{{ item.name }}</option></select></label><label class="registration-wide">Cargo description *<input v-model="form.cargo" required maxlength="180" placeholder="What are you shipping?" /></label><label>Gross weight (kg) *<input v-model="form.weightKg" type="number" min="0.001" max="100000" step="0.001" required /></label><label>Number of packages *<input v-model="form.packages" type="number" min="1" max="1000000" step="1" required /></label></fieldset>
        <fieldset class="employee-fields container-route-fields" :disabled="mode === 'view' || saving"><legend class="container-section-title">02 &middot; Route &amp; schedule</legend><label>Origin port *<select v-model="form.originPortId" required><option value="">Select origin</option><option v-if="selected && !ports.some(x => x.id === selected.originPortId)" :value="selected.originPortId">{{ selected.originPortName }}</option><option v-for="item in ports" :key="item.id" :value="item.id">{{ item.name }} ({{ item.code }})</option></select></label><label>Destination port *<select v-model="form.destinationPortId" required><option value="">Select destination</option><option v-if="selected && !ports.some(x => x.id === selected.destinationPortId)" :value="selected.destinationPortId">{{ selected.destinationPortName }}</option><option v-for="item in ports" :key="item.id" :value="item.id" :disabled="item.id === Number(form.originPortId)">{{ item.name }} ({{ item.code }})</option></select></label><label>Expected departure *<BusinessDatePicker v-model="form.departure"  required /></label><label>Expected arrival *<BusinessDatePicker v-model="form.expectedArrival"  :min="form.departure" required /></label><label class="registration-wide">Your reference (optional)<input v-model="form.reference" maxlength="60" placeholder="Purchase order or booking reference" /></label></fieldset>
        <section class="container-files" aria-labelledby="container-files-title"><h3 id="container-files-title">03 &middot; Photos &amp; reference documents</h3><p class="muted container-help">Photos: JPEG/PNG/WebP. Documents: PDF, DOCX, XLS, XLSX or TXT. Up to 10 MB per file.</p><div v-if="mode !== 'view'" class="photo-buttons"><button type="button" class="btn secondary small-btn" @click="photoInput.click()" :disabled="saving"><ImagePlus :size="16" />Add container photos</button><button type="button" class="btn secondary small-btn" @click="documentInput.click()" :disabled="saving"><FilePlus2 :size="16" />Add reference documents</button></div><input ref="photoInput" class="sr-only" type="file" accept="image/jpeg,image/png,image/webp" multiple @change="chooseFiles($event,1)" :disabled="saving || mode === 'view'" aria-label="Choose container photos" /><input ref="documentInput" class="sr-only" type="file" accept=".pdf,.docx,.xls,.xlsx,.txt" multiple @change="chooseFiles($event,2)" :disabled="saving" aria-label="Choose reference documents" />
          <ul class="container-file-list"><li v-for="file in selected?.files || []" :key="file.id" :class="{ 'file-pending-removal': removals.includes(file.id) }"><div><strong>{{ file.fileName }}</strong><small>{{ file.kind === 1 ? 'Photo' : 'Reference document' }} &middot; {{ size(file.sizeBytes) }}{{ removals.includes(file.id) ? ' - marked for removal' : '' }}</small></div><button type="button" class="icon-btn" @click="download(file)" :disabled="saving" :aria-label="'Open ' + file.fileName"><Eye :size="16" /></button><button v-if="mode !== 'view' && !file.trackingEventId" type="button" class="text-btn" :disabled="saving" @click="removals.includes(file.id) ? removals = removals.filter(id => id !== file.id) : removals.push(file.id)">{{ removals.includes(file.id) ? 'Undo' : 'Remove' }}</button></li><li v-for="pending in files" :key="pending.key"><div><strong>{{ pending.file.name }}</strong><small>{{ size(pending.file.size) }} &middot; Pending upload</small></div><button type="button" class="icon-btn" @click="files = files.filter(x => x.key !== pending.key)" :disabled="saving" :aria-label="'Remove pending ' + pending.file.name"><Trash2 :size="16" /></button></li></ul><p v-if="!selected?.files.length && !files.length" class="muted container-help">No files attached.</p></section>
      </template><p v-if="formError" class="form-error" role="alert">{{ formError }}</p></div><div class="modal-footer"><button class="btn secondary" type="button" @click="close" :disabled="saving">{{ mode === 'view' ? 'Close' : 'Cancel' }}</button><button v-if="mode !== 'view'" class="btn" :class="mode === 'cancel' ? 'danger' : 'primary'" type="submit" :disabled="saving || (mode === 'create' && (optionsLoading || !shippers.length || ports.length < 2))"><LoaderCircle v-if="saving" class="spin" :size="16" />{{ saving ? 'Saving registration and files...' : mode === 'cancel' ? 'Cancel registration' : 'Save registration' }}</button></div></form></dialog>
    <ContainerTrackingDialog ref="trackingDialog" @saved="trackingSaved" @error="error = $event" />
    <ContainerFileViewer v-if="viewingFile" :container-id="selected?.id" :file="viewingFile" @close="viewingFile = null" />
  </section>
</template>

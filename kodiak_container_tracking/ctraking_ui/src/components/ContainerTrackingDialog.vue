<script setup>
import BusinessDatePicker from './BusinessDatePicker.vue';
import { nextTick, onBeforeUnmount, ref } from 'vue';
import { X, ShieldCheck, FilePlus2, Eye, Trash2, LoaderCircle, ArrowRight, Clock3 } from 'lucide-vue-next';
import { useAuth } from '../composables/useAuth.js';
import { containerApi } from '../services/container-api.js';
import { trackingStatuses, validateTrackingFiles } from '../services/container-tracking.js';
import ContainerFileViewer from './ContainerFileViewer.vue';
const emit = defineEmits(['saved', 'error']); const api = containerApi(useAuth().request);
const dialog = ref(null); const item = ref(null); const form = ref({}); const pending = ref([]); const input = ref(null); const viewing = ref(null);
const busy = ref(false); const saving = ref(false); const error = ref(''); let disposed = false;
function localDateTime() { const now = new Date(); return new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0,23); }
async function open(row) {
  if (busy.value) return; busy.value = true; error.value = ''; pending.value = []; viewing.value = null;
  try {
    const record = await api.get(row.id); if (disposed) return; item.value = record;
    form.value = { containerNumber:record.containerNumber || '', status:record.trackingStatus || 1, eventAt:localDateTime(), location:record.trackingEvents?.[0]?.location || '', vesselName:record.trackingEvents?.[0]?.vesselName || '', expectedArrival:record.expectedArrival, note:'', isOnHold:record.isOnHold || false };
    await nextTick(); if (!disposed) dialog.value.showModal();
  } catch (failure) { emit('error', failure.message); } finally { if (!disposed) busy.value = false; }
}
function close() { if (!saving.value) { viewing.value = null; dialog.value?.close(); } }
function choose(event) {
  const files = Array.from(event.target.files || []); event.target.value = '';
  const allFiles = [...pending.value, ...files]; const message = validateTrackingFiles(allFiles);
  if (message) { error.value = message; return; } error.value = ''; pending.value = allFiles;
}
function previewPending(file) { viewing.value = {fileName:file.name, blob:file}; }
async function save() {
  if (saving.value) return; saving.value = true; error.value = '';
  try {
    const fileError = validateTrackingFiles(pending.value); if (fileError) throw new Error(fileError);
    const payload = {...form.value, containerNumber:form.value.containerNumber.trim(), location:form.value.location.trim(), vesselName:form.value.vesselName.trim(), note:form.value.note.trim(), eventAt:new Date(form.value.eventAt).toISOString()};
    const updated = await api.track(item.value.id, item.value.version, payload, pending.value);
    if (!disposed) { pending.value = []; dialog.value.close(); emit('saved', updated); }
  } catch (failure) { if (!disposed) error.value = failure.message; }
  finally { if (!disposed) saving.value = false; }
}
onBeforeUnmount(() => { disposed = true; dialog.value?.close(); });
defineExpose({open});
</script>
<template>
  <dialog ref="dialog" class="employee-dialog tracking-update-dialog" aria-labelledby="tracking-dialog-title" @cancel="saving ? $event.preventDefault() : close()"><div class="modal-header"><div><p class="eyebrow">KODIAK OPERATIONS</p><h2 id="tracking-dialog-title">Update container tracking</h2></div><button type="button" class="icon-btn" @click="close" :disabled="saving" aria-label="Close tracking update"><X :size="20" /></button></div>
    <form @submit.prevent="save"><div class="modal-body"><div class="tracking-audit-note"><ShieldCheck :size="18" /><span>{{ item?.code }} &middot; Changes and supporting files are saved to the shipment timeline and audit history.</span></div>
      <fieldset class="employee-fields tracking-update-fields" :disabled="saving"><legend class="sr-only">Tracking update</legend>
        <label>Container number<input v-model="form.containerNumber" maxlength="24" placeholder="MSCU 782341-6" /></label><label>Official status<select v-model="form.status" required><option v-for="status in trackingStatuses" :key="status.id" :value="status.id">{{ status.name }}</option></select></label>
        <label>Event date &amp; time<BusinessDatePicker v-model="form.eventAt" time  required /><small>Shown in your local time zone.</small></label><label>Reported location<input v-model="form.location" required maxlength="200" placeholder="Port, city or current location" /></label>
        <label>Vessel name<input v-model="form.vesselName" maxlength="150" placeholder="Vessel name (optional)" /></label><label>Expected arrival<BusinessDatePicker v-model="form.expectedArrival"  :min="item?.departure" required /></label>
        <label class="registration-wide">Operational note<textarea v-model="form.note" maxlength="2000" rows="3" placeholder="What changed? Add context for the shipper and consignee."></textarea></label>
        <label class="tracking-hold registration-wide"><input v-model="form.isOnHold" type="checkbox" /><span>Place shipment on hold<small>Tracking stays visible. Release the hold before marking delivered.</small></span></label>
      </fieldset>
      <section class="tracking-upload-section" aria-labelledby="tracking-upload-title"><div class="tracking-section-heading"><div><h3 id="tracking-upload-title">Supporting documents</h3><p>Images, PDF or Excel &middot; 10 MB per file &middot; up to 10 files / 50 MB per update.</p></div><button type="button" class="btn secondary small-btn" @click="input.click()" :disabled="saving"><FilePlus2 :size="16" />Add files</button></div><input ref="input" type="file" class="sr-only" accept=".jpg,.jpeg,.png,.webp,.pdf,.xls,.xlsx" multiple @change="choose" :disabled="saving" aria-label="Upload tracking documents" />
        <ul v-if="pending.length" class="container-file-list"><li v-for="(file,index) in pending" :key="index"><div><strong>{{ file.name }}</strong><small>{{ (file.size / 1024).toFixed(0) }} KB &middot; Ready to upload</small></div><button type="button" class="icon-btn" @click="previewPending(file)" :disabled="saving" :aria-label="'Preview ' + file.name"><Eye :size="16" /></button><button type="button" class="icon-btn" @click="pending.splice(index,1)" :disabled="saving" :aria-label="'Remove ' + file.name"><Trash2 :size="16" /></button></li></ul><p v-else class="tracking-no-files">Attach delivery proof, port documents, photos or a shipment workbook.</p>
        <details v-if="item?.files.length" class="tracking-existing-files"><summary>View existing uploaded files ({{ item.files.length }})</summary><ul class="container-file-list"><li v-for="file in item.files" :key="file.id"><div><strong>{{ file.fileName }}</strong><small>Uploaded {{ new Date(file.uploadedOn).toLocaleString() }}</small></div><button type="button" class="icon-btn" @click="viewing = file" :aria-label="'View ' + file.fileName"><Eye :size="16" /></button></li></ul></details>
      </section><p v-if="error" class="form-error" role="alert">{{ error }}</p></div><div class="modal-footer"><button type="button" class="btn secondary" @click="close" :disabled="saving">Cancel</button><button type="submit" class="btn primary" :disabled="saving"><LoaderCircle v-if="saving" class="spin" :size="16" />{{ saving ? 'Saving tracking and files...' : 'Save tracking update' }}<ArrowRight v-if="!saving" :size="16" /></button></div></form>
    <ContainerFileViewer v-if="viewing" :container-id="item?.id" :file="viewing" @close="viewing = null" />
  </dialog>
</template>

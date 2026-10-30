<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue';
import { X, Download, LoaderCircle, FileSpreadsheet, FileText } from 'lucide-vue-next';
import { useAuth } from '../composables/useAuth.js';
import { containerApi } from '../services/container-api.js';
import { filePreviewType, readWorkbookPreview } from '../services/container-tracking.js';
const props = defineProps({ containerId: Number, file: {type:Object,required:true} });
const emit = defineEmits(['close']); const api = containerApi(useAuth().request);
const dialog = ref(null); const loading = ref(true); const error = ref(''); const url = ref(''); const sheets = ref([]); const activeSheet = ref(0); let disposed = false;
const type = computed(() => filePreviewType(props.file)); const sheet = computed(() => sheets.value[activeSheet.value]);
function close() { dialog.value?.close(); emit('close'); }
function download() { const link = document.createElement('a'); link.href = url.value; link.download = props.file.fileName; document.body.appendChild(link); link.click(); link.remove(); }
onMounted(async () => {
  await nextTick(); if (disposed) return; dialog.value.showModal();
  try {
    const blob = props.file.blob || await api.download(props.containerId, props.file.id); if (disposed) return;
    url.value = URL.createObjectURL(blob);
    if (type.value === 'excel') { const workbook = await readWorkbookPreview(await blob.arrayBuffer()); if (!disposed) sheets.value = workbook; }
  } catch { if (!disposed) error.value = 'This file could not be previewed. You can download it if the download button is available.'; }
  finally { if (!disposed) loading.value = false; }
});
onBeforeUnmount(() => { disposed = true; dialog.value?.close(); if (url.value) URL.revokeObjectURL(url.value); });
</script>
<template>
  <dialog ref="dialog" class="employee-dialog file-viewer-dialog" aria-labelledby="file-viewer-title" @cancel.prevent="close"><div class="modal-header"><div><p class="eyebrow">UPLOADED FILE</p><h2 id="file-viewer-title">{{ file.fileName }}</h2></div><button type="button" class="icon-btn" @click="close" aria-label="Close file viewer"><X :size="20" /></button></div>
    <div class="file-viewer-body"><p v-if="loading" class="file-viewer-loading" role="status"><LoaderCircle class="spin" :size="22" />Opening file...</p><p v-if="error" class="form-error" role="alert">{{ error }}</p>
      <template v-if="!loading && !error"><img v-if="type === 'image'" class="file-viewer-image" :src="url" :alt="file.fileName" /><iframe v-else-if="type === 'pdf'" class="file-viewer-pdf" :src="url" :title="file.fileName" />
        <div v-else-if="type === 'excel'" class="workbook-preview"><div class="workbook-sheet-tabs" role="tablist" aria-label="Workbook sheets"><button v-for="(item,index) in sheets" :key="item.name" type="button" role="tab" :aria-selected="index === activeSheet" :class="{active:index === activeSheet}" @click="activeSheet = index"><FileSpreadsheet :size="14" />{{ item.name }}</button></div><p class="workbook-preview-note">Read-only preview &middot; up to 200 rows and 30 columns per sheet. Download for the complete workbook.</p><div v-if="sheet" class="workbook-table-wrap"><table class="workbook-table"><caption class="sr-only">{{ sheet.name }}</caption><tbody><tr v-for="(row,index) in sheet.rows" :key="index"><th scope="row">{{ index + 1 }}</th><td v-for="(cell,column) in row" :key="column">{{ cell }}</td></tr></tbody></table></div><p v-if="!sheet?.rows.length" class="muted">This sheet is empty.</p></div>
        <p v-else class="file-viewer-loading"><FileText :size="25" />Download this file to open it in its original application.</p>
      </template></div><div class="modal-footer"><button type="button" class="btn secondary" @click="close">Close</button><button type="button" class="btn primary" @click="download" :disabled="!url || loading"><Download :size="16" />Download original</button></div>
  </dialog>
</template>

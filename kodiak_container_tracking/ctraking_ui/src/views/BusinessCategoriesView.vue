<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue';
import { Plus, RefreshCw, Eye, Pencil, Trash2, X, Tags, ChevronLeft, ChevronRight, LoaderCircle } from 'lucide-vue-next';
import { useAuth } from '../composables/useAuth.js';
import { businessCategoryApi } from '../services/business-category-api.js';
const api = businessCategoryApi(useAuth().request);
const records = ref([]); const total = ref(0); const page = ref(1); const pageSize = ref(25);
const loading = ref(false); const opening = ref(false); const saving = ref(false); const error = ref(''); const formError = ref(''); const success = ref('');
const dialog = ref(null); const mode = ref('create'); const selected = ref(null); const form = ref({ code: '', name: '', isActive: true });
let disposed = false;
const pages = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)));
const dialogTitle = computed(() => ({ create: 'Add business category', view: 'Business category details', edit: 'Edit business category', delete: 'Delete business category' })[mode.value]);
async function load() {
  loading.value = true; error.value = '';
  try { const result = await api.list(page.value, pageSize.value); if (!disposed) { records.value = result.items; total.value = result.totalCount; } }
  catch (failure) { if (!disposed) error.value = failure.message; } finally { if (!disposed) loading.value = false; }
}
async function open(action, row) {
  if (opening.value) return;
  opening.value = true; formError.value = ''; error.value = '';
  try {
    const item = row ? await api.get(row.id) : null;
    if (disposed) return;
    selected.value = item; mode.value = action;
    form.value = { code: item?.code || '', name: item?.name || '', isActive: item?.isActive ?? true };
    await nextTick(); if (!disposed) dialog.value.showModal();
  } catch (failure) { if (!disposed) error.value = failure.message; } finally { if (!disposed) opening.value = false; }
}
function close() { if (!saving.value) dialog.value.close(); }
async function save() {
  if (saving.value || mode.value === 'view') return;
  saving.value = true; formError.value = ''; success.value = '';
  try {
    if (mode.value === 'delete') await api.remove(selected.value.id, selected.value.version);
    else { const payload = { code: form.value.code.trim().toUpperCase(), name: form.value.name.trim(), isActive: form.value.isActive }; if (mode.value === 'create') await api.create(payload); else await api.update(selected.value.id, selected.value.version, payload); }
    if (!disposed) { dialog.value.close(); success.value = mode.value === 'delete' ? 'Business category deactivated.' : mode.value === 'create' ? 'Business category created.' : 'Business category updated.'; await load(); }
  } catch (failure) { if (!disposed) formError.value = failure.message; } finally { if (!disposed) saving.value = false; }
}
async function changePage(value) { page.value = value; await load(); }
onMounted(load);
onBeforeUnmount(() => { disposed = true; dialog.value?.close(); });
</script>
<template>
  <section aria-labelledby="business-categories-title">
    <div class="page-heading"><div><p class="eyebrow">MANAGEMENT</p><h1 id="business-categories-title">Business categories</h1><p>Maintain the categories available to shippers and registration.</p></div><button class="btn primary" @click="open('create')" :disabled="opening"><Plus :size="17" />Add category</button></div>
    <p v-if="success" class="employee-notice" role="status">{{ success }}</p><p v-if="error" class="form-error" role="alert">{{ error }}</p>
    <section class="panel employee-panel" :aria-busy="loading"><div class="panel-heading"><div><h2>Category directory <span class="count-pill">{{ total }}</span></h2><p>Active categories are available for new selections.</p></div><button class="btn secondary small-btn" @click="load" :disabled="loading"><RefreshCw :size="14" />Refresh</button></div>
      <div v-if="loading" class="employee-empty" role="status"><LoaderCircle class="spin" :size="24" />Loading business categories...</div>
      <div v-else-if="!records.length" class="employee-empty"><Tags :size="35" /><h3>{{ error ? 'Unable to load categories' : 'No business categories yet' }}</h3><p>{{ error ? 'Use Refresh to try again.' : 'Add a category to make it available to shippers.' }}</p></div>
      <div v-else class="employee-table-wrap"><table class="employee-table category-table"><caption class="sr-only">Business category directory</caption><thead><tr><th scope="col">Code</th><th scope="col">Category name</th><th scope="col">Status</th><th scope="col">Actions</th></tr></thead><tbody><tr v-for="category in records" :key="category.id"><td><span class="employee-code">{{ category.code }}</span></td><td><button class="employee-name" @click="open('view',category)" :disabled="opening">{{ category.name }}</button></td><td><span class="badge" :class="category.isActive ? 'green' : 'gray'">{{ category.isActive ? 'Active' : 'Inactive' }}</span></td><td><div class="employee-row-actions"><button class="icon-btn" @click="open('view',category)" :disabled="opening" :aria-label="'View ' + category.name" title="View"><Eye :size="16" /></button><button class="icon-btn" @click="open('edit',category)" :disabled="opening" :aria-label="'Edit ' + category.name" title="Edit"><Pencil :size="16" /></button><button class="icon-btn delete-action" @click="open('delete',category)" :disabled="opening || !category.isActive" :aria-label="'Delete ' + category.name" title="Delete (deactivate)"><Trash2 :size="16" /></button></div></td></tr></tbody></table></div>
      <div class="employee-pagination"><label>Rows per page<select v-model="pageSize" @change="changePage(1)" :disabled="loading"><option :value="10">10</option><option :value="25">25</option><option :value="50">50</option></select></label><span>Page {{ page }} of {{ pages }}</span><div><button class="icon-btn" aria-label="Previous page" :disabled="page <= 1 || loading" @click="changePage(page - 1)"><ChevronLeft :size="18" /></button><button class="icon-btn" aria-label="Next page" :disabled="page >= pages || loading" @click="changePage(page + 1)"><ChevronRight :size="18" /></button></div></div>
    </section>
    <dialog ref="dialog" class="employee-dialog category-dialog" aria-labelledby="category-dialog-title" @cancel="saving && $event.preventDefault()"><div class="modal-header"><div><p class="eyebrow">BUSINESS CATEGORIES</p><h2 id="category-dialog-title">{{ dialogTitle }}</h2></div><button class="icon-btn" type="button" @click="close" :disabled="saving" aria-label="Close dialog"><X :size="20" /></button></div>
      <form @submit.prevent="save"><div class="modal-body"><template v-if="mode === 'delete'"><p class="employee-delete-copy">Delete <strong>{{ selected?.name }}</strong> ({{ selected?.code }})?</p><p class="muted employee-delete-copy">This marks the category as inactive and removes it from new selections. Existing shipper links are retained. Use Edit to reactivate it later.</p></template><fieldset v-else class="category-fields" :disabled="mode === 'view' || saving"><legend class="sr-only">Category details</legend><label>Code *<input v-model="form.code" required maxlength="50" pattern="[A-Za-z0-9_-]+" title="Use letters, numbers, underscores or hyphens." autocomplete="off" /><small>Unique code. Saved in uppercase.</small></label><label>Category name *<input v-model="form.name" required maxlength="300" autocomplete="off" /></label><label>Status<select v-model="form.isActive"><option :value="true">Active</option><option :value="false">Inactive</option></select></label></fieldset><p v-if="formError" class="form-error" role="alert">{{ formError }}</p></div>
      <div class="modal-footer"><button class="btn secondary" type="button" @click="close" :disabled="saving">{{ mode === 'view' ? 'Close' : 'Cancel' }}</button><button v-if="mode !== 'view'" class="btn" :class="mode === 'delete' ? 'danger' : 'primary'" type="submit" :disabled="saving"><LoaderCircle v-if="saving" class="spin" :size="15" />{{ saving ? 'Saving...' : mode === 'delete' ? 'Delete category' : 'Save category' }}</button></div></form>
    </dialog>
  </section>
</template>

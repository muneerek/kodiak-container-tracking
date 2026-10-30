<script setup>
import BusinessDatePicker from '../components/BusinessDatePicker.vue';
import { computed, nextTick, onMounted, ref } from 'vue';
import { Plus, RefreshCw, Eye, Pencil, Trash2, X, UsersRound, ChevronLeft, ChevronRight, LoaderCircle } from 'lucide-vue-next';
import { useAuth } from '../composables/useAuth.js';
import EmployeePhotoEditor from '../components/EmployeePhotoEditor.vue';
import { employeeApi } from '../services/employee-api.js';
const { session, request } = useAuth();
const api = employeeApi(request);
const isSuperAdmin = computed(() => session.value?.user.roles?.some(role => role.code === 'SUPERADMIN'));
const photoEditorKey = ref(0);
const pendingPhoto = ref(undefined); const cropping = ref(false);
const records = ref([]); const total = ref(0); const page = ref(1); const pageSize = ref(25);
const loading = ref(false); const opening = ref(false); const saving = ref(false); const error = ref(''); const formError = ref(''); const success = ref(''); const masterError = ref('');
const designations = ref([]); const organizations = ref([]); const dialog = ref(null); const mode = ref('create'); const selected = ref(null); const form = ref({});
const pages = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)));
const dialogTitle = computed(() => ({create:'Add employee',view:'Employee details',edit:'Edit employee',delete:'Delete employee'})[mode.value]);
const today = new Date().toLocaleDateString('en-CA');
function masterName(list, id) { return list.find(item => item.id === id)?.name || (id ? '#' + id : '-'); }
async function load() {
  loading.value = true; error.value = '';
  try { const result = await api.list(page.value, pageSize.value); records.value = result.items; total.value = result.totalCount; }
  catch (failure) { error.value = failure.message; } finally { loading.value = false; }
}
async function loadMasters() {
  masterError.value = '';
  try { [designations.value, organizations.value] = await Promise.all([api.masters('Designations'), api.masters('Organizations')]); }
  catch { masterError.value = 'Could not load designations and organizations. Retry before adding or editing employees.'; }
}
async function open(action, row) {
  if (opening.value) return;
  photoEditorKey.value++; pendingPhoto.value = undefined; cropping.value = false; opening.value = true; error.value = ''; formError.value = ''; mode.value = action;
  try {
    selected.value = row ? await api.get(row.uniqueId) : null;
    const item = selected.value;
    form.value = { firstName: item?.firstName || '', lastName: item?.lastName || '', dob: item?.dob || '', emailId: item?.emailId || '', logo: item?.logo || '', userStatus: item?.userStatus || 1, designationId: item?.designationId || '', orgId: item?.orgId || '' };
    await nextTick(); dialog.value.showModal();
  } catch (failure) { error.value = failure.message; } finally { opening.value = false; }
}
function close() { if (!saving.value) dialog.value.close(); }
async function save() {
  if (saving.value || cropping.value) return;
  saving.value = true; formError.value = ''; success.value = '';
  const originalAction = mode.value; let profileSaved = false;
  try {
    if (mode.value === 'delete') await api.remove(selected.value.uniqueId, selected.value.version);
    else {
      const payload = { ...form.value, logo: selected.value?.logo || null, firstName: form.value.firstName.trim(), lastName: form.value.lastName.trim() || null, dob: form.value.dob || null, emailId: form.value.emailId.trim() || null,  designationId: Number(form.value.designationId) || null, orgId: Number(form.value.orgId) || null };
      const result = mode.value === 'create' ? await api.create(payload) : await api.update(selected.value.uniqueId, selected.value.version, payload);
      selected.value = result; profileSaved = true;
      if (pendingPhoto.value !== undefined) {
        // If upload fails after creation, retry against this saved employee rather than creating a duplicate.
        mode.value = 'edit';
        const updated = pendingPhoto.value === null ? await api.removePhoto(result.uniqueId, result.version) : await api.uploadPhoto(result.uniqueId, result.version, pendingPhoto.value);
        selected.value = updated; pendingPhoto.value = undefined;
      }
    }
    dialog.value.close(); success.value = originalAction === 'delete' ? 'Employee marked as terminated.' : originalAction === 'create' ? 'Employee created successfully.' : 'Employee updated successfully.';
    await load();
  } catch (failure) { formError.value = profileSaved && pendingPhoto.value !== undefined ? 'Employee details were saved, but the photo was not saved. ' + failure.message : failure.message; } finally { saving.value = false; }
}
async function changePage(value) { page.value = value; await load(); }
onMounted(() => { load(); loadMasters(); });
</script>
<template>
  <section aria-labelledby="employees-heading">
    <div class="page-heading"><div><p class="eyebrow">MANAGEMENT</p><h1 id="employees-heading">Employees</h1><p>Manage your team, employee details, and status.</p></div><button class="btn primary" @click="open('create')" :disabled="opening || !!masterError"><Plus :size="17" />Add employee</button></div>
    <p v-if="success" class="employee-notice" role="status">{{ success }}</p>
    <p v-if="error" class="form-error" role="alert">{{ error }}</p>
    <div v-if="masterError" class="form-error" role="alert">{{ masterError }}<button class="text-btn" @click="loadMasters">Retry</button></div>
    <section class="panel employee-panel" :aria-busy="loading">
      <div class="panel-heading"><div><h2>Employee directory <span class="count-pill">{{ total }}</span></h2><p>Employee codes are generated automatically.</p></div><button class="btn secondary small-btn" @click="load" :disabled="loading"><RefreshCw :size="14" />Refresh</button></div>
      <div v-if="loading" class="employee-empty" role="status"><LoaderCircle class="spin" :size="24" />Loading employees...</div>
      <div v-else-if="!records.length" class="employee-empty"><UsersRound :size="35" /><h3>{{ error ? 'Unable to load employees' : 'No employees yet' }}</h3><p>{{ error ? 'Use Refresh to try again.' : 'Add your first employee to get started.' }}</p></div>
      <div v-else class="employee-table-wrap"><table class="employee-table"><caption class="sr-only">Employee directory</caption><thead><tr><th scope="col">Employee</th><th scope="col">Code / ID</th><th scope="col">Email</th><th scope="col">Designation</th><th scope="col">Status</th><th scope="col">Actions</th></tr></thead><tbody><tr v-for="employee in records" :key="employee.uniqueId"><td><button class="employee-name" @click="open('view',employee)" :disabled="opening">{{ employee.name }}</button></td><td><span class="employee-code">{{ employee.uniqueCode }}</span><small class="employee-id">ID: {{ employee.uniqueId }}</small></td><td>{{ employee.emailId || '-' }}</td><td>{{ masterName(designations,employee.designationId) }}</td><td><span class="badge" :class="employee.userStatus === 1 ? 'green' : 'red'">{{ employee.userStatus === 1 ? 'Active' : 'Terminated' }}</span></td><td><div class="employee-row-actions"><button class="icon-btn" @click="open('view', employee)" :disabled="opening" :aria-label="'View ' + employee.name" title="View"><Eye :size="16" /></button><button class="icon-btn" @click="open('edit', employee)" :disabled="opening || !!masterError" :aria-label="'Edit ' + employee.name" title="Edit"><Pencil :size="16" /></button><button class="icon-btn delete-action" @click="open('delete', employee)" :disabled="opening || employee.userStatus === 2" :aria-label="'Delete ' + employee.name" title="Delete (terminate)"><Trash2 :size="16" /></button></div></td></tr></tbody></table></div>
      <div class="employee-pagination"><label>Rows per page<select v-model="pageSize" @change="changePage(1)" :disabled="loading"><option :value="10">10</option><option :value="25">25</option><option :value="50">50</option></select></label><span>Page {{ page }} of {{ pages }}</span><div><button class="icon-btn" aria-label="Previous page" :disabled="page <= 1 || loading" @click="changePage(page - 1)"><ChevronLeft :size="18" /></button><button class="icon-btn" aria-label="Next page" :disabled="page >= pages || loading" @click="changePage(page + 1)"><ChevronRight :size="18" /></button></div></div>
    </section>
    <dialog ref="dialog" class="employee-dialog" aria-labelledby="employee-dialog-title" @cancel="saving && $event.preventDefault()">
      <div class="modal-header"><div><p class="eyebrow">EMPLOYEE DIRECTORY</p><h2 id="employee-dialog-title">{{ dialogTitle }}</h2></div><button class="icon-btn" type="button" @click="close" :disabled="saving" aria-label="Close dialog"><X :size="20" /></button></div>
      <form @submit.prevent="save">
        <div class="modal-body">
          <template v-if="mode === 'delete'"><p class="employee-delete-copy">Delete <strong>{{ selected?.name }}</strong> ({{ selected?.uniqueCode }})?</p><p class="muted employee-delete-copy">This marks the employee as Terminated and prevents linked accounts from signing in. The record and audit history are retained. You can reactivate the employee using Edit.</p></template>
          <template v-else>
            <p v-if="selected" class="employee-record-code">{{ selected.uniqueCode }} &middot; Employee ID {{ selected.uniqueId }}</p>
            <EmployeePhotoEditor :key="photoEditorKey" :employee-id="selected?.uniqueId" :path="selected?.logo" :readonly="mode === 'view' || saving" @change="pendingPhoto = $event" @editing="cropping = $event" />
            <fieldset class="employee-fields" :disabled="mode === 'view' || saving"><legend class="sr-only">Employee details</legend>
              <label>First name *<input v-model="form.firstName" required maxlength="300" autocomplete="given-name" /></label><label>Last name<input v-model="form.lastName" maxlength="300" autocomplete="family-name" /></label>
              <label>Date of birth<BusinessDatePicker v-model="form.dob"  :max="today" /></label><label>Email address<input v-model="form.emailId" type="email" maxlength="150" autocomplete="email" /></label>
              <label>Designation<select v-model="form.designationId"><option value="">Not selected</option><option v-for="item in designations.filter(d => isSuperAdmin || d.code !== 'SUPERADMIN')" :key="item.id" :value="item.id" :disabled="!item.isActive">{{ item.name }}{{ item.isActive ? '' : ' (inactive)' }}</option></select></label>
              <label>Organization<select v-model="form.orgId"><option value="">Not selected</option><option v-for="item in organizations" :key="item.id" :value="item.id" :disabled="!item.isActive">{{ item.name }}{{ item.isActive ? '' : ' (inactive)' }}</option></select></label>
              <label>Status<select v-model="form.userStatus"><option :value="1">Active</option><option :value="2">Terminated</option></select></label>
            </fieldset>
          </template>
          <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>
        </div>
        <div class="modal-footer"><button class="btn secondary" type="button" @click="close" :disabled="saving">{{ mode === 'view' ? 'Close' : 'Cancel' }}</button><button v-if="mode !== 'view'" class="btn" :class="mode === 'delete' ? 'danger' : 'primary'" type="submit" :disabled="saving || cropping"><LoaderCircle v-if="saving" class="spin" :size="15" />{{ saving ? 'Saving...' : mode === 'delete' ? 'Delete employee' : 'Save employee' }}</button></div>
      </form>
    </dialog>
  </section>
</template>

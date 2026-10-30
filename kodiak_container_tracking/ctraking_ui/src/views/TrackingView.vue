<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { Ship, Search, Download, Plus, ArrowUpRight, ArrowLeft, ArrowRight, MapPin, Clock3, Check, ShieldCheck, Anchor, Package, RefreshCw, LoaderCircle, Eye, FileText, X, ChevronLeft, ChevronRight, CircleCheck, PauseCircle } from 'lucide-vue-next';
import { useAuth } from '../composables/useAuth.js';
import { containerApi } from '../services/container-api.js';
import { containerCsv } from '../services/container-export.js';
import { trackingStatuses, trackingStatusName } from '../services/container-tracking.js';
import { journeyStatus, journeyProgress, trackingFilters, fetchTrackingExport } from '../services/tracking-directory.js';
import ContainerTrackingDialog from '../components/ContainerTrackingDialog.vue';
import ContainerFileViewer from '../components/ContainerFileViewer.vue';
const props = defineProps({ initialStatus: {type:String,default:''}, initialContainerId: Number, initialShipperId: Number });
const emit = defineEmits(['register']); const {session,request} = useAuth(); const api = containerApi(request);
const isAdmin = computed(() => session.value?.user.userType === 1); const canRegister = computed(() => [1,3].includes(session.value?.user.userType));
const records = ref([]); const total = ref(0); const page = ref(1); const pageSize = ref(12);
const search = ref(''); const status = ref(props.initialStatus); const shipperId = ref(props.initialShipperId || ''); const shippers = ref([]); const optionsError = ref('');
const loading = ref(false); const exporting = ref(false); const error = ref(''); const success = ref('');
const detailOpen = ref(false); const detailLoading = ref(false); const detailError = ref(''); const selectedId = ref(null); const selected = ref(null);
const updateDialog = ref(null); const viewingFile = ref(null); let disposed = false; let loadId = 0; let detailLoadId = 0; let timer;
const pages = computed(() => Math.max(1,Math.ceil(total.value/pageSize.value)));
const filters = computed(() => trackingFilters(search.value,status.value,shipperId.value));
const latest = computed(() => selected.value?.trackingEvents?.[0]);
const typeNames = {'20STD':'20 ft Standard','40STD':'40 ft Standard','40HC':'40 ft High Cube','20REEFER':'20 ft Reefer','40REEFER':'40 ft Reefer'};
function date(value,full = false) { if (!value) return '-'; const parsed = new Date(value.length === 10 ? value+'T00:00:00' : value); return Number.isNaN(parsed.getTime()) ? '-' : parsed.toLocaleDateString(undefined,{day:'2-digit',month:'short',...(full ? {year:'numeric'} : {})}); }
function dateTime(value) { return value ? new Date(value).toLocaleString(undefined,{day:'2-digit',month:'short',year:'numeric',hour:'2-digit',minute:'2-digit'}) : '-'; }
function overdue(item) { const now = new Date();const today = new Date(now.getFullYear(),now.getMonth(),now.getDate());return item.status !== 2 && item.trackingStatus !== 6 && new Date(item.expectedArrival+'T00:00:00') < today; }
function latestLocation(item) { return item.trackingEvents?.[0]?.location || 'Awaiting first tracking update'; }
async function load() {
  const current=++loadId;loading.value=true;error.value='';
  try { const result=await api.trackingList(page.value,pageSize.value,filters.value);if(!disposed && current===loadId){records.value=result.items;total.value=result.totalCount;} }
  catch(failure){if(!disposed && current===loadId)error.value=failure.message;}
  finally{if(!disposed && current===loadId)loading.value=false;}
}
async function loadShippers(){optionsError.value='';try{const items=await api.options('tracking-shippers');if(!disposed)shippers.value=items;}catch{if(!disposed)optionsError.value='Shipper choices are unavailable.';}}
function filterNow(){clearTimeout(timer);page.value=1;load();}
watch(search,()=>{clearTimeout(timer);timer=setTimeout(filterNow,300);});watch([status,shipperId],filterNow);
async function changePage(value){page.value=value;await load();}
async function openJourney(row){
  const current=++detailLoadId;selectedId.value=row.id;detailOpen.value=true;detailLoading.value=true;detailError.value='';selected.value=null;viewingFile.value=null;
  try{const item=await api.get(row.id);if(!disposed && current===detailLoadId)selected.value=item;}
  catch(failure){if(!disposed && current===detailLoadId)detailError.value=failure.message;}
  finally{if(!disposed && current===detailLoadId)detailLoading.value=false;}
}
function back(){detailLoadId++;detailOpen.value=false;selected.value=null;viewingFile.value=null;detailError.value='';}
async function updated(item){if(disposed)return;if(selected.value?.id===item.id)selected.value=item;success.value='Tracking update and supporting files saved.';await load();}
async function exportJourneys(){
  if(exporting.value)return;exporting.value=true;error.value='';const currentFilters={...filters.value};
  try{const items=await fetchTrackingExport(api,currentFilters);if(disposed)return;const url=URL.createObjectURL(new Blob([containerCsv(items)],{type:'text/csv;charset=utf-8;'}));const link=document.createElement('a');link.href=url;link.download='tracking-journeys.csv';document.body.appendChild(link);link.click();link.remove();setTimeout(()=>URL.revokeObjectURL(url),1000);}
  catch(failure){if(!disposed)error.value=failure.message;}finally{if(!disposed)exporting.value=false;}
}
onMounted(()=>{load();loadShippers();if(props.initialContainerId)openJourney({id:props.initialContainerId});});onBeforeUnmount(()=>{disposed=true;loadId++;detailLoadId++;clearTimeout(timer);});
</script>
<template>
  <section class="tracking-workspace" aria-labelledby="tracking-page-title">
    <p v-if="success" class="employee-notice" role="status">{{ success }}</p>
    <template v-if="!detailOpen">
      <div class="page-heading tracking-page-heading"><div><p class="eyebrow">FOLLOW EVERY MILESTONE</p><h1 id="tracking-page-title">Every journey, in view.</h1><p>Track your shipments, follow each milestone, and keep everyone informed.</p></div><button v-if="canRegister" type="button" class="btn primary" @click="emit('register')"><Plus :size="17" />Register container</button></div>
      <form class="tracking-toolbar" @submit.prevent="filterNow"><label class="tracking-search"><Search :size="17" aria-hidden="true" /><input v-model="search" type="search" maxlength="100" placeholder="Search container, booking, route or shipper..." aria-label="Search tracking journeys" /><button v-if="search" type="button" class="icon-btn" aria-label="Clear tracking search" @click="search = ''"><X :size="15" /></button></label><select v-model="status" aria-label="Filter journeys by status"><option value="">All statuses</option><option v-for="stage in trackingStatuses" :key="stage.id" :value="String(stage.id)">{{ stage.name }}</option><option value="hold">On hold</option><option value="cancelled">Cancelled</option></select><select v-if="session?.user.userType !== 3" v-model="shipperId" aria-label="Filter journeys by shipper" :disabled="!!optionsError"><option value="">All shippers</option><option v-for="shipper in shippers" :key="shipper.id" :value="shipper.id">{{ shipper.name }}</option></select><button type="button" class="btn secondary" @click="exportJourneys" :disabled="exporting"><LoaderCircle v-if="exporting" class="spin" :size="16" /><Download v-else :size="16" />{{ exporting ? 'Exporting...' : 'Export' }}</button></form>
      <div class="tracking-results-heading"><span role="status">{{ total }} {{ total === 1 ? 'journey' : 'journeys' }}{{ search || status || shipperId ? ' found' : ' in your workspace' }}</span><button type="button" class="text-btn" @click="load" :disabled="loading"><RefreshCw :size="13" />Refresh</button></div><p v-if="optionsError" class="tracking-options-note">{{ optionsError }} <button type="button" class="text-btn" @click="loadShippers">Retry</button></p><p v-if="error" class="form-error" role="alert">{{ error }}</p>
      <div v-if="loading" class="tracking-empty" role="status"><LoaderCircle class="spin" :size="26" /><p>Loading your journeys...</p></div>
      <div v-else-if="!records.length" class="tracking-empty"><Ship :size="36" /><h2>{{ error ? 'Unable to load journeys' : 'No journeys found' }}</h2><p>{{ error ? 'Refresh to try again.' : 'Register a container or try a different search or filter.' }}</p><button v-if="search || status || shipperId" type="button" class="btn secondary small-btn" @click="search='';status='';shipperId=''">Clear filters</button></div>
      <div v-else class="tracking-journey-grid">
        <article v-for="item in records" :key="item.id" class="panel tracking-journey-card">
          <div class="tracking-card-top"><span class="tracking-ship-icon"><Ship :size="21" /></span><span class="badge" :class="journeyStatus(item).tone">{{ journeyStatus(item).name }}</span></div>
          <h2><button type="button" @click="openJourney(item)">{{ item.containerNumber || 'Pending allocation' }}</button></h2><p class="tracking-card-reference">{{ item.code }} &middot; {{ item.shipperName }}</p>
          <div class="tracking-card-route"><div><strong>{{ item.originPortName }}</strong><small>{{ item.originPortCode }}</small></div><span class="tracking-route-line"><Ship :size="16" /></span><div><strong>{{ item.destinationPortName }}</strong><small>{{ item.destinationPortCode }}</small></div></div>
          <div class="tracking-mini-progress" :aria-label="'Current milestone: ' + trackingStatusName(item.trackingStatus)"><span v-for="stage in trackingStatuses" :key="stage.id" :class="{complete:stage.id <= journeyProgress(item),held:item.isOnHold}" /></div>
          <p class="tracking-card-location"><MapPin :size="12" /><span>{{ latestLocation(item) }}</span></p>
          <div class="tracking-card-footer"><div><small>{{ overdue(item) ? 'Arrival overdue' : 'Expected arrival' }}</small><strong :class="{'tracking-overdue':overdue(item)}">{{ date(item.expectedArrival) }}</strong></div><div class="tracking-card-actions"><button v-if="isAdmin && item.status === 1" type="button" class="tracking-card-update" @click="updateDialog.open(item)" :aria-label="'Update status for ' + item.code">Update status</button><button type="button" class="text-btn" @click="openJourney(item)" :aria-label="'View timeline for ' + item.code">View timeline <ArrowUpRight :size="14" /></button></div></div>
        </article>
      </div>
      <div class="employee-pagination tracking-pagination"><label>Journeys per page<select v-model="pageSize" @change="filterNow" :disabled="loading"><option :value="12">12</option><option :value="24">24</option><option :value="48">48</option></select></label><span>Page {{ page }} of {{ pages }}</span><div><button type="button" class="icon-btn" aria-label="Previous tracking page" @click="changePage(page-1)" :disabled="loading || page <= 1"><ChevronLeft :size="18" /></button><button type="button" class="icon-btn" aria-label="Next tracking page" @click="changePage(page+1)" :disabled="loading || page >= pages"><ChevronRight :size="18" /></button></div></div>
    </template>
    <template v-else>
      <button type="button" class="tracking-back" @click="back"><ArrowLeft :size="15" />All journeys</button>
      <div v-if="detailLoading" class="tracking-empty" role="status"><LoaderCircle class="spin" :size="26" /><p>Opening shipment timeline...</p></div>
      <div v-else-if="detailError" class="tracking-empty"><h1 id="tracking-page-title">Unable to open journey</h1><p class="form-error" role="alert">{{ detailError }}</p><button type="button" class="btn secondary" @click="openJourney({id:selectedId})">Retry</button></div>
      <template v-else-if="selected">
        <div class="page-heading tracking-detail-heading"><div><p class="eyebrow">CONTAINER JOURNEY &middot; {{ selected.code }}</p><h1 id="tracking-page-title">{{ selected.containerNumber || 'Pending allocation' }} <span class="badge" :class="journeyStatus(selected).tone">{{ journeyStatus(selected).name }}</span></h1><p>{{ typeNames[selected.containerType] || selected.containerType }} <span>/</span> {{ selected.shipperName }} <span>/</span> {{ selected.cargo }}</p></div><button v-if="isAdmin && selected.status === 1" type="button" class="btn primary" @click="updateDialog.open(selected)"><RefreshCw :size="16" />Update status</button></div>
        <section class="tracking-journey-hero" aria-label="Shipment route and progress"><div class="tracking-hero-top"><span><i></i>{{ selected.status === 2 ? 'REGISTRATION CANCELLED' : selected.isOnHold ? 'SHIPMENT ON HOLD' : selected.trackingStatus === 6 ? 'JOURNEY COMPLETED' : 'JOURNEY IN PROGRESS' }}</span><span><Clock3 :size="13" />{{ latest ? 'Last reported ' + dateTime(latest.eventAt) : 'Registered ' + dateTime(selected.createdOn) }}</span></div>
          <div class="tracking-hero-route"><div><small>ORIGIN PORT</small><h2>{{ selected.originPortName }}</h2><span class="tracking-port-code">{{ selected.originPortCode }}</span></div><div class="tracking-hero-connection"><span></span><div><Ship :size="28" /></div><span></span><p>{{ latest?.vesselName || 'Vessel not reported' }}</p></div><div><small>DESTINATION PORT</small><h2>{{ selected.destinationPortName }}</h2><span class="tracking-port-code">{{ selected.destinationPortCode }}</span></div></div>
          <ol class="tracking-hero-stages"><li v-for="stage in trackingStatuses" :key="stage.id" :class="{done:stage.id < journeyProgress(selected),current:stage.id === journeyProgress(selected)}" :aria-current="stage.id === journeyProgress(selected) ? 'step' : undefined"><span><Check v-if="stage.id < journeyProgress(selected)" :size="13" /><template v-else>{{ stage.id }}</template></span>{{ stage.name }}</li></ol>
        </section>
        <div class="tracking-detail-grid"><section class="panel tracking-timeline-panel" aria-labelledby="journey-timeline-title"><div class="tracking-panel-header"><div><h2 id="journey-timeline-title">Tracking timeline</h2><p>Every recorded milestone, from registration to delivery</p></div><span class="count-pill">{{ (selected.trackingEvents?.length || 0) + 1 }} events</span></div>
            <ol class="tracking-full-timeline"><li v-for="(entry,index) in selected.trackingEvents" :key="entry.id" :class="{latest:index === 0}"><span class="tracking-event-node"><CircleCheck v-if="entry.status === 6" :size="17" /><Ship v-else-if="entry.status === 3" :size="17" /><MapPin v-else :size="17" /></span><div class="tracking-event-content"><div class="tracking-event-title"><h3>{{ trackingStatusName(entry.status) }} <span v-if="index === 0" class="tracking-latest-label">LATEST UPDATE</span><span v-if="entry.isOnHold" class="badge amber">On hold</span></h3><time :datetime="entry.eventAt">{{ dateTime(entry.eventAt) }}</time></div><p class="tracking-event-location"><MapPin :size="12" />{{ entry.location }}<span v-if="entry.vesselName"> &middot; {{ entry.vesselName }}</span></p><p v-if="entry.note" class="tracking-event-description">{{ entry.note }}</p><div v-if="entry.files.length" class="tracking-event-documents"><button v-for="file in entry.files" :key="file.id" type="button" @click="viewingFile = file"><FileText :size="14" />{{ file.fileName }}<Eye :size="13" /></button></div><small>Recorded {{ dateTime(entry.recordedAt) }} &middot; Expected arrival {{ date(entry.expectedArrival,true) }}</small></div></li>
              <li><span class="tracking-event-node"><Package :size="17" /></span><div class="tracking-event-content"><div class="tracking-event-title"><h3>Registration created</h3><time :datetime="selected.createdOn">{{ dateTime(selected.createdOn) }}</time></div><p class="tracking-event-location"><MapPin :size="12" />{{ selected.originPortName }}</p><p class="tracking-event-description">Shipment registered for {{ selected.shipperName }}.</p><small>Booking reference {{ selected.code }}</small></div></li>
            </ol><div class="tracking-timeline-note"><ShieldCheck :size="15" />Updates are entered by Kodiak operations. Event times are shown in your local time zone.</div>
          </section>
          <aside class="tracking-detail-sidebar"><section class="panel tracking-shipment-details"><div class="tracking-panel-header"><h2>Shipment details</h2><Package :size="19" /></div><dl><div><dt>Booking reference</dt><dd>{{ selected.code }}</dd></div><div><dt>Customer reference</dt><dd>{{ selected.reference || 'Not provided' }}</dd></div><div><dt>Shipper</dt><dd>{{ selected.shipperName }}</dd></div><div><dt>Consignee</dt><dd>{{ selected.consigneeName || 'Not linked' }}</dd></div><div><dt>Container type</dt><dd>{{ typeNames[selected.containerType] || selected.containerType }}</dd></div><div><dt>Gross weight</dt><dd>{{ Number(selected.weightKg).toLocaleString() }} kg</dd></div><div><dt>Packages</dt><dd>{{ Number(selected.packages).toLocaleString() }}</dd></div><div><dt>Expected departure</dt><dd>{{ date(selected.departure,true) }}</dd></div><div><dt>Expected arrival</dt><dd :class="{'tracking-overdue':overdue(selected)}">{{ date(selected.expectedArrival,true) }}</dd></div><div><dt>Latest reported location</dt><dd>{{ latestLocation(selected) }}</dd></div></dl></section>
            <section class="panel tracking-documents-panel"><div class="tracking-panel-header"><h2>Shipment documents</h2><span class="count-pill">{{ selected.files.length }}</span></div><button v-for="file in selected.files" :key="file.id" type="button" class="tracking-document-row" @click="viewingFile = file"><FileText :size="17" /><span><strong>{{ file.fileName }}</strong><small>{{ (file.sizeBytes/1024).toFixed(0) }} KB &middot; {{ date(file.uploadedOn) }}</small></span><Eye :size="15" /></button><p v-if="!selected.files.length" class="tracking-no-documents">No documents uploaded yet.</p></section>
            <section class="tracking-team-note"><Anchor :size="24" /><h3>People behind every milestone.</h3><p>{{ isAdmin ? 'Keep partners informed with a clear status, a reported location, and supporting documents.' : 'Your Kodiak operations team maintains this timeline. Shippers and consignees share the same journey updates.' }}</p><span><ShieldCheck :size="14" />{{ isAdmin ? 'You can update this shipment' : 'Tracking managed by Kodiak' }}</span></section>
          </aside>
        </div>
      </template>
    </template>
    <ContainerTrackingDialog ref="updateDialog" @saved="updated" @error="error = $event; detailError = detailOpen ? $event : ''" />
    <ContainerFileViewer v-if="viewingFile && selected" :container-id="selected.id" :file="viewingFile" @close="viewingFile = null" />
  </section>
</template>

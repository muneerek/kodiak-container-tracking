<script setup>
import {computed,nextTick,onBeforeUnmount,onMounted,ref} from 'vue';
import {Plus,MapPin,Globe2,MapPinned} from 'lucide-vue-next';
import {useAuth} from '../composables/useAuth.js';
import {locationMasterApi} from '../services/location-master-api.js';
import LocationMastersView from './LocationMastersView.vue';
const {request}=useAuth();
const tabs=[{kind:'ShippingPorts',label:'Ports',singular:'port',icon:MapPin},{kind:'States',label:'States / regions',singular:'state / region',icon:MapPinned},{kind:'Countries',label:'Countries',singular:'country',icon:Globe2}];
const active=ref('ShippingPorts'),counts=ref({}),master=ref(null),navigation=ref(null);
const selected=computed(()=>tabs.find(tab=>tab.kind===active.value));
let disposed=false;
async function selectTab(kind){active.value=kind;await nextTick();navigation.value?.querySelector('#location-tab-'+kind)?.focus();}
function navigate(event){const keys=['ArrowRight','ArrowLeft','Home','End'];if(!keys.includes(event.key))return;event.preventDefault();const index=tabs.findIndex(tab=>tab.kind===active.value);const next=event.key==='Home'?0:event.key==='End'?tabs.length-1:(index+(event.key==='ArrowRight'?1:tabs.length-1))%tabs.length;selectTab(tabs[next].kind);}
onMounted(async()=>{await Promise.allSettled(tabs.map(async tab=>{const result=await locationMasterApi(tab.kind,request).list(1,1);if(!disposed && counts.value[tab.kind]===undefined)counts.value[tab.kind]=result.totalCount;}));});
onBeforeUnmount(()=>{disposed=true;});
</script>
<template>
 <section class="location-directory" aria-labelledby="location-directory-title">
  <div class="page-heading"><div><p class="eyebrow">YOUR GLOBAL DIRECTORY</p><h1 id="location-directory-title">Locations, connected.</h1><p>Maintain the countries, states, and ports that power your shipping routes.</p></div><button class="btn primary" @click="master?.create()" :disabled="!master?.canCreate"><Plus :size="18"/>Add {{selected.singular}}</button></div>
  <div ref="navigation" class="location-tabs" role="tablist" aria-label="Location directory navigation" @keydown="navigate"><button v-for="tab in tabs" :key="tab.kind" :id="'location-tab-'+tab.kind" role="tab" :aria-selected="active===tab.kind" :tabindex="active===tab.kind?0:-1" aria-controls="location-directory-panel" :class="{selected:active===tab.kind}" @click="selectTab(tab.kind)"><component :is="tab.icon" :size="18"/><span>{{tab.label}}</span><small>{{counts[tab.kind]??'—'}}</small></button></div>
  <div id="location-directory-panel" role="tabpanel" :aria-labelledby="'location-tab-'+active"><LocationMastersView ref="master" :key="active" :kind="active" embedded @loaded="counts[active]=$event"/></div>
 </section>
</template>
<style scoped>
.location-tabs{display:flex;gap:20px;border-bottom:1px solid #dbe7ee;margin:12px 0 26px;overflow-x:auto}.location-tabs button{display:flex;align-items:center;gap:10px;padding:15px 7px 16px;white-space:nowrap;font-size:13px;color:#7c94a5;border-bottom:2px solid transparent}.location-tabs button.selected{border-bottom-color:#049bcb;color:#078cbe}.location-tabs small{font-size:10px;background:#e7f0f7;color:#5a8099;min-width:22px;text-align:center;padding:3px 5px;border-radius:4px}.location-tabs button:focus-visible{outline:2px solid #1aa8d3;outline-offset:-4px;border-radius:5px}html[data-theme="dark"] .location-tabs{border-color:#91b5d229}html[data-theme="dark"] .location-tabs button{color:#b1c7dd}html[data-theme="dark"] .location-tabs button.selected{color:#00d9ed;border-color:#00d9ed}html[data-theme="dark"] .location-tabs small{background:#a4c9e51a;color:#c0dced}@media(max-width:600px){.location-tabs{gap:10px;margin-bottom:18px}.location-tabs button{font-size:12px;gap:7px;padding-inline:5px}}
</style>

<script setup>
import { nextTick, onBeforeUnmount, ref, useId, watch } from 'vue';
import { ChevronDown, X, LoaderCircle } from 'lucide-vue-next';
import { registrationApi } from '../services/registration-api.js';
const props = defineProps({ modelValue: { type: [Number, String], default: null }, initialOption: Object, disabled: Boolean });
const emit = defineEmits(['update:modelValue']);
const id = useId(); const input = ref(null); const text = ref(props.initialOption ? props.initialOption.name + ' (' + props.initialOption.code + ')' : ''); const selected = ref(props.initialOption || null); const options = ref([]);
const expanded = ref(false); const loading = ref(false); const error = ref(''); const active = ref(-1); const page = ref(1); const total = ref(0);
let timer; let request; let generation = 0; let disposed = false;
async function load(nextPage = 1) {
  clearTimeout(timer); request?.abort(); request = new AbortController(); const current = ++generation;
  loading.value = true; error.value = '';
  if (nextPage === 1) { options.value = []; active.value = -1; }
  try {
    const result = await registrationApi.options('consignees', selected.value ? '' : text.value.trim(), nextPage, request.signal);
    if (current !== generation || disposed) return;
    options.value = nextPage === 1 ? result.items : [...options.value, ...result.items]; total.value = result.totalCount; page.value = nextPage;
  } catch (failure) { if (current === generation && !disposed && failure.name !== 'AbortError') error.value = failure.message; }
  finally { if (current === generation && !disposed) loading.value = false; }
}
function open() { if (props.disabled || expanded.value) return; expanded.value = true; load(); }
function close() { expanded.value = false; active.value = -1; clearTimeout(timer); request?.abort(); generation++; loading.value = false; }
function change(event) {
  text.value = event.target.value; selected.value = null; emit('update:modelValue', null);
  expanded.value = true; options.value = []; active.value = -1; error.value = ''; loading.value = true;
  clearTimeout(timer); request?.abort(); generation++; timer = setTimeout(() => load(), 300);
}
function select(item) { selected.value = item; text.value = item.name + ' (' + item.code + ')'; emit('update:modelValue', item.id); input.value.focus(); close(); }
function clear() { selected.value = null; text.value = ''; emit('update:modelValue', null); input.value.focus(); close(); }
async function keydown(event) {
  if (event.key === 'Escape' && expanded.value) { event.preventDefault(); event.stopPropagation(); close(); }
  else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    event.preventDefault(); if (!expanded.value) open();
    if (!options.value.length) return;
    active.value = (active.value + (event.key === 'ArrowDown' ? 1 : -1) + options.value.length) % options.value.length;
    await nextTick(); document.getElementById(id + '-option-' + active.value)?.scrollIntoView({ block: 'nearest' });
  } else if (event.key === 'Enter' && expanded.value) { event.preventDefault(); if (options.value[active.value]) select(options.value[active.value]); }
}
function focusout(event) { if (!event.currentTarget.contains(event.relatedTarget)) close(); }
watch(() => props.disabled, value => { if (value) close(); });
onBeforeUnmount(() => { disposed = true; close(); });
</script>
<template>
  <div class="consignee-picker registration-wide" @focusout="focusout">
    <label :for="id">Consignee (optional)</label>
    <div class="consignee-picker-input"><input :id="id" ref="input" :value="text" @input="change" @focus="open" @click="open" @keydown="keydown" :disabled="disabled" type="text" role="combobox" aria-autocomplete="list" :aria-expanded="expanded" :aria-controls="expanded ? id + '-options' : undefined" :aria-activedescendant="expanded && active >= 0 ? id + '-option-' + active : undefined" :aria-describedby="id + '-help'" autocomplete="off" maxlength="100" placeholder="Search and select by company name or code" /><button v-if="text" type="button" class="icon-btn" @mousedown.prevent @click="clear" :disabled="disabled" aria-label="Clear consignee"><X :size="16" /></button><ChevronDown v-else :size="16" aria-hidden="true" />
    <div v-if="expanded" class="consignee-picker-dropdown">
      <p v-if="loading && !options.length" class="consignee-picker-status" role="status"><LoaderCircle class="spin" :size="16" />Searching consignees...</p>
      <div v-if="error" class="consignee-picker-status" role="alert">{{ error }}<button type="button" class="text-btn" @click="load()">Retry</button></div>
      <ul :id="id + '-options'" class="consignee-picker-options" role="listbox" aria-label="Matching consignees"><li v-for="(item,index) in options" :key="item.id" role="presentation"><button :id="id + '-option-' + index" type="button" role="option" :aria-selected="selected?.id === item.id" :class="{ highlighted: active === index }" tabindex="-1" @mousedown.prevent @click="select(item)"><strong>{{ item.name }}</strong><small>{{ item.code }}</small></button></li></ul>
      <p v-if="!loading && !error && !options.length" class="consignee-picker-status" role="status">No matching active consignees. You may leave this field blank.</p>
      <button v-if="options.length < total && !error" type="button" class="consignee-picker-more text-btn" @click="load(page + 1)" :disabled="loading">{{ loading ? 'Loading...' : 'Show more matches' }}</button>
    </div>
    </div>
    <p :id="id + '-help'" class="consignee-picker-help">{{ selected ? 'Selected: ' + selected.name : 'No consignee selected. Choose a result or leave this field blank.' }}</p>
  </div>
</template>

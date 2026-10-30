<script setup>
import { onBeforeUnmount, ref, watch } from 'vue';
import { Building2 } from 'lucide-vue-next';
import { useAuth } from '../composables/useAuth.js';
const props = defineProps({ id: Number, path: String, resource: String, name: String });
const { request } = useAuth();
const source = ref(''); let controller; let generation = 0; let objectUrl = '';
function clear() { if (objectUrl) URL.revokeObjectURL(objectUrl); objectUrl = ''; source.value = ''; }
watch(() => [props.id, props.path, props.resource], async () => {
  const current = ++generation; controller?.abort(); clear();
  if (!props.path) return;
  if (props.resource === 'shippers' && props.id) {
    controller = new AbortController();
    try {
      const blob = await request('/shippers/' + props.id + '/logo', { responseType: 'blob', signal: controller.signal });
      if (current === generation) { objectUrl = URL.createObjectURL(blob); source.value = objectUrl; }
    } catch { /* Keep the company icon when a logo is unavailable. */ }
  } else if (/^(https?:\/\/|\/(?!\/)|data:image\/(png|jpeg|webp|gif);base64,)/i.test(props.path)) {
    source.value = props.path;
  }
}, { immediate: true });
onBeforeUnmount(() => { generation++; controller?.abort(); clear(); });
</script>
<template><span class="directory-logo"><img v-if="source" :src="source" :alt="name + ' logo'" @error="source = ''" /><Building2 v-else :size="23" aria-hidden="true" /></span></template>

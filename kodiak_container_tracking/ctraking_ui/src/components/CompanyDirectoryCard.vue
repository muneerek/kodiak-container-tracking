<script setup>
import CompanyDirectoryLogo from './CompanyDirectoryLogo.vue';
import { MapPin, Building2, Mail, Phone, UserRound, ArrowRight, Pencil, Trash2 } from 'lucide-vue-next';
const props = defineProps({ item: { type: Object, required: true }, country: String, category: String, resource: String, busy: Boolean, canDelete: { type: Boolean, default: true } });
defineEmits(['view', 'edit', 'delete']);
</script>
<template>
  <article class="panel partner-card directory-card">
    <div class="partner-top"><CompanyDirectoryLogo :id="item.uniqueId" :path="item.logo" :resource="resource" :name="item.companyName" /><span class="badge" :class="item.status === 1 ? 'green' : 'red'">{{ item.status === 1 ? 'Active' : 'Blacklisted' }}</span></div>
    <h2><button class="directory-company-name" :disabled="busy" @click="$emit('view')">{{ item.companyName }}</button></h2>
    <p>{{ category || item.uniqueCode }}</p>
    <div class="partner-meta directory-meta">
      <span><MapPin :size="14" />{{ country === '-' ? 'Country not specified' : country }}</span>
      <span v-if="item.consignee"><Building2 :size="14" />{{ item.consignee.companyName }}</span>
      <span v-if="item.lastName"><UserRound :size="14" />{{ item.lastName }}</span>
      <span><Mail :size="14" /><a :href="'mailto:' + item.emailId">{{ item.emailId }}</a></span>
      <span v-if="item.phoneNumber"><Phone :size="14" /><a :href="'tel:' + item.phoneNumber">{{ item.phoneNumber }}</a></span>
    </div>
    <div class="partner-footer directory-footer"><button class="text-btn" :disabled="busy" @click="$emit('view')">View details <ArrowRight :size="14" /></button><div class="employee-row-actions"><button class="icon-btn" :disabled="busy" :aria-label="'Edit ' + item.companyName" title="Edit" @click="$emit('edit')"><Pencil :size="16" /></button><button v-if="canDelete" class="icon-btn delete-action" :disabled="busy || item.status === 2" :aria-label="'Delete ' + item.companyName" title="Delete (blacklist)" @click="$emit('delete')"><Trash2 :size="16" /></button></div></div>
  </article>
</template>

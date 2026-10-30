<script setup>
import {computed,nextTick,onBeforeUnmount,ref} from 'vue';
import {X,Building2,LoaderCircle,Save} from 'lucide-vue-next';
import {useAuth} from '../composables/useAuth.js';
import {companyProfileApi} from '../services/company-profile-api.js';
import {shipperApi} from '../services/shipper-api.js';
import EmployeePhotoEditor from './EmployeePhotoEditor.vue';
const {session,request,updateCompanyDetails}=useAuth();const api=companyProfileApi(request);const logos=shipperApi(request);
const dialog=ref(null),record=ref(null),countries=ref([]),form=ref({}),opening=ref(false),saving=ref(false),error=ref(''),success=ref(''),pendingLogo=ref(undefined),cropping=ref(false),editorKey=ref(0);let disposed=false;
const isShipper=computed(()=>session.value?.user.userType===3);
async function open(){if(opening.value||saving.value)return;opening.value=true;record.value=null;error.value='';success.value='';pendingLogo.value=undefined;cropping.value=false;
 await nextTick();dialog.value.showModal();
 try{const [item,options]=await Promise.all([api.get(),api.countries()]);if(disposed)return;record.value=item;countries.value=options;form.value={companyName:item.companyName,lastName:item.lastName||'',emailId:item.emailId,phoneNumber:item.phoneNumber||'',address:item.address,countryId:item.countryId||''};editorKey.value++;}
 catch(failure){if(!disposed)error.value=failure.message;}finally{if(!disposed)opening.value=false;}}
function close(){if(!saving.value)dialog.value.close();}
async function save(){if(saving.value||cropping.value)return;saving.value=true;error.value='';success.value='';let detailsSaved=false;
 try{const item=await api.update(record.value.version,{companyName:form.value.companyName.trim(),lastName:form.value.lastName.trim()||null,emailId:form.value.emailId.trim(),phoneNumber:form.value.phoneNumber.trim()||null,address:form.value.address.trim(),countryId:Number(form.value.countryId)||null});record.value=item;detailsSaved=true;updateCompanyDetails(item);
 if(isShipper.value&&pendingLogo.value!==undefined){record.value=pendingLogo.value===null?await logos.removeLogo(item.uniqueId,item.version):await logos.uploadLogo(item.uniqueId,item.version,pendingLogo.value);pendingLogo.value=undefined;updateCompanyDetails(record.value);}
 if(!disposed)success.value='Your profile has been updated.';
 }catch(failure){if(!disposed)error.value=(detailsSaved?'Profile details were saved. The logo could not be saved; try again. ':'')+failure.message;}finally{if(!disposed)saving.value=false;}}
onBeforeUnmount(()=>{disposed=true;dialog.value?.close();});defineExpose({open});
</script>
<template><dialog ref="dialog" class="employee-dialog company-profile-dialog" aria-labelledby="company-profile-title" @cancel="saving && $event.preventDefault()"><div class="modal-header"><div><p class="eyebrow">MY COMPANY PROFILE</p><h2 id="company-profile-title">{{isShipper?'My shipper profile':'My consignee profile'}}</h2></div><button type="button" class="icon-btn" @click="close" :disabled="saving" aria-label="Close profile"><X :size="20"/></button></div>
 <div v-if="opening" class="employee-empty" role="status"><LoaderCircle class="spin" :size="25"/>Loading your profile...</div>
 <form v-else @submit.prevent="save"><div class="modal-body"><template v-if="record"><p class="employee-record-code">{{record.uniqueCode}} &middot; {{session?.user.userName}}</p><EmployeePhotoEditor v-if="isShipper" :key="editorKey" :employee-id="record.uniqueId" :path="record.logo" resource="shippers" label="Company logo" :readonly="saving" @change="pendingLogo=$event" @editing="cropping=$event"/>
 <fieldset class="employee-fields" :disabled="saving"><legend class="sr-only">Company profile details</legend><label>Company name *<input v-model="form.companyName" required maxlength="300" autocomplete="organization"/></label><label>Contact person<input v-model="form.lastName" maxlength="300" autocomplete="name"/></label><label>Email address *<input v-model="form.emailId" type="email" required maxlength="150" autocomplete="email"/></label><label>Phone number<input v-model="form.phoneNumber" type="tel" maxlength="30" autocomplete="tel"/></label><label class="registration-wide">Country<select v-model="form.countryId"><option value="">Not selected</option><option v-if="form.countryId&&!countries.some(item=>item.id===Number(form.countryId))" :value="form.countryId">Current country #{{form.countryId}}</option><option v-for="country in countries" :key="country.id" :value="country.id" :disabled="country.isActive === false">{{country.name}}{{country.isActive === false ? ' (inactive)' : ''}}</option></select></label><label class="registration-wide">Address *<textarea v-model="form.address" required maxlength="2000" rows="3" autocomplete="street-address"></textarea></label></fieldset></template>
 <p v-if="error" class="form-error" role="alert">{{error}}</p><button v-if="error&&!record" type="button" class="text-btn" @click="open">Retry</button><p v-if="success" class="employee-notice" role="status">{{success}}</p></div><div class="modal-footer"><button type="button" class="btn secondary" @click="close" :disabled="saving">Close</button><button v-if="record" type="submit" class="btn primary" :disabled="saving||cropping"><LoaderCircle v-if="saving" class="spin" :size="16"/><Save v-else :size="16"/>{{saving?'Saving...':'Save profile'}}</button></div></form>
 </dialog></template>

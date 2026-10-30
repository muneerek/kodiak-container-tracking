<script setup>
import {ref,onMounted,onBeforeUnmount,watch} from 'vue';
import flatpickr from 'flatpickr';
import 'flatpickr/dist/flatpickr.css';
import {CalendarDays} from 'lucide-vue-next';
defineOptions({inheritAttrs:false});
const props=defineProps({modelValue:String,min:String,max:String,time:Boolean});
const emit=defineEmits(['update:modelValue']);
const input=ref(null),host=ref(null);let picker;
const boundary=value=>value?flatpickr.parseDate(value,'Y-m-d'):undefined;
const storageFormat=()=>props.time?'Y-m-d\\TH:i:S':'Y-m-d';
function syncValue(){if(!picker)return;picker.setDate(props.modelValue||'',false,storageFormat());}
onMounted(()=>{
 picker=flatpickr(input.value,{
  appendTo:host.value,disableMobile:true,allowInput:true,
  dateFormat:props.time?'d M Y, H:i':'d M Y',enableTime:props.time,time_24hr:true,
  minuteIncrement:1,minDate:boundary(props.min),maxDate:boundary(props.max),
  locale:{firstDayOfWeek:1},ariaDateFormat:'l, j F Y',
  onChange(dates){emit('update:modelValue',dates.length?picker.formatDate(dates[0],storageFormat()):'');},
  onOpen(){if(input.value.matches(':disabled'))picker.close();},
 });syncValue();
});
watch(()=>props.modelValue,syncValue);
watch(()=>props.min,value=>picker?.set('minDate',boundary(value)));
watch(()=>props.max,value=>picker?.set('maxDate',boundary(value)));
onBeforeUnmount(()=>picker?.destroy());
</script>
<template><span ref="host" class="business-date-field"><input ref="input" v-bind="$attrs" type="text" autocomplete="off" :placeholder="time?'Select date and time':'Select date'"/><CalendarDays :size="18" aria-hidden="true"/></span></template>
<style>
.business-date-field{position:relative;display:block;min-width:0;width:100%}.business-date-field>input{width:100%;padding-right:42px!important;cursor:pointer}.business-date-field>svg{position:absolute;right:15px;top:50%;transform:translateY(-50%);pointer-events:none;color:#7397ad}.business-date-field .flatpickr-calendar{font-family:inherit;border:1px solid #dbe7ef;border-radius:12px;box-shadow:0 12px 35px #17384e24;padding:10px;width:328px;max-width:calc(100vw - 40px);z-index:30}.business-date-field .flatpickr-calendar.open{top:calc(100% + 7px)!important;left:0!important;right:auto!important}.business-date-field .flatpickr-calendar:before,.business-date-field .flatpickr-calendar:after{display:none}.business-date-field .flatpickr-months{margin-bottom:8px}.business-date-field .flatpickr-current-month{font-size:14px}.business-date-field .flatpickr-current-month input.cur-year{padding:0!important;border:0!important;height:auto!important;font-size:14px!important;box-shadow:none!important;background:transparent!important}.business-date-field .flatpickr-current-month .flatpickr-monthDropdown-months{font-size:14px!important;padding:0!important;border:0!important;background:transparent!important;width:auto!important;height:auto!important}.business-date-field .flatpickr-weekday{color:#8096a6;font-weight:500}.business-date-field .flatpickr-day{border-radius:8px;color:#35576c}.business-date-field .flatpickr-day.selected{background:#0788bd;border-color:#0788bd;color:white}.business-date-field .flatpickr-day.today{border-color:#0788bd}.business-date-field .flatpickr-day.flatpickr-disabled{color:#c7d1d9}.business-date-field .flatpickr-time input{padding:0!important;border:0!important;background:transparent!important;box-shadow:none!important;height:100%!important;font-size:14px!important}.business-date-field .flatpickr-time{margin-top:8px}.business-date-field .flatpickr-calendar.arrowRight.open{left:auto!important;right:0!important}html[data-theme="dark"] .business-date-field .flatpickr-calendar{background:#233e56;border-color:#48647a}html[data-theme="dark"] .business-date-field :is(.flatpickr-day,.flatpickr-month,.flatpickr-weekday,.flatpickr-current-month select,.flatpickr-current-month input,.flatpickr-time input,.flatpickr-time-separator){color:#dceaf5}html[data-theme="dark"] .business-date-field .flatpickr-day:hover{background:#385b76}html[data-theme="dark"] .business-date-field .flatpickr-day.flatpickr-disabled{color:#6e879a}
</style>

<script setup>
import { Sun, Moon } from 'lucide-vue-next';
defineProps({ modelValue: String });
defineEmits(['update:modelValue']);
</script>

<template>
  <div class="theme-toggle" role="group" aria-label="Color theme">
    <button type="button" :aria-pressed="modelValue === 'light'" :class="{ selected: modelValue === 'light' }" @click="$emit('update:modelValue', 'light')" title="Light theme"><Sun :size="14"/><span>Light</span></button>
    <button type="button" :aria-pressed="modelValue === 'dark'" :class="{ selected: modelValue === 'dark' }" @click="$emit('update:modelValue', 'dark')" title="Dark theme"><Moon :size="14"/><span>Dark</span></button>
  </div>
</template>

<style>
.theme-toggle{display:inline-flex;gap:3px;align-items:center;padding:3px;border:1px solid #dbe5e6;border-radius:8px;background:#edf2f3;flex-shrink:0}
.theme-toggle button{display:flex;align-items:center;gap:5px;padding:6px 8px;border-radius:5px;color:#70838e;font-size:10px;font-weight:600;line-height:1.2}
.theme-toggle button.selected{background:#fff;color:#087f70;box-shadow:0 1px 4px #193b4214}
.theme-toggle button:hover{color:#087f70}
html[data-theme="dark"] .theme-toggle{background:#18365055;border-color:#8ab8d33d}
html[data-theme="dark"] .theme-toggle button{color:#a7c1d9}
html[data-theme="dark"] .theme-toggle button.selected{background:#00c9e31f;color:#00e0f5;box-shadow:inset 0 0 0 1px #00cfe53d}
html[data-theme="dark"] .theme-toggle button:hover{color:#6aeaf7}
.login-top{gap:15px;align-items:center;flex-wrap:wrap}
html[data-theme="light"]{color-scheme:light}
@media(max-width:520px){.topbar-actions .demo-tag{display:none}.theme-toggle button{padding:6px}.topbar-actions{gap:5px}.login-top{gap:10px;justify-content:center}}
</style>

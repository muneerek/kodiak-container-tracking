import { createApp } from 'vue';
import App from './App.vue';
import '@fontsource-variable/dm-sans';
import '@fontsource-variable/manrope';
import './style.css';
import './theme.css';
import './light-blue.css';
try { document.documentElement.dataset.theme = localStorage.getItem('kodiak-theme') === 'light' ? 'light' : 'dark'; }
catch { document.documentElement.dataset.theme = 'dark'; }
createApp(App).mount('#app');

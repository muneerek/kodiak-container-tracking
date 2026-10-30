<script setup>
import { computed, reactive, ref, watch, onBeforeUnmount } from 'vue';
import { Boxes, ShieldCheck, LogOut, CheckCircle2, LayoutDashboard, UsersRound, ChevronDown, ChevronRight, Menu, X, LockKeyhole, Tags, Building2, Truck, MapPinned, Route } from 'lucide-vue-next';
import { useAuth } from '../composables/useAuth.js';
import EmployeesView from './EmployeesView.vue';
import BusinessCategoriesView from './BusinessCategoriesView.vue';
import ConsigneesView from './ConsigneesView.vue';
import ShippersView from './ShippersView.vue';
import LocationDirectoryView from './LocationDirectoryView.vue';
import ContainersView from './ContainersView.vue';
import TrackingView from './TrackingView.vue';
import OverviewView from './OverviewView.vue';
import CompanyProfileDialog from '../components/CompanyProfileDialog.vue';
const companyProfile = ref(null);
const trackingInitialStatus = ref(''); const trackingInitialId = ref(null); const trackingInitialShipper = ref(null);
const containerCreateOnOpen = ref(false);
const { session, signOut, request } = useAuth();
const busy = ref(false); const error = ref(''); const mobileNav = ref(false); const page = ref('overview');
const expanded = reactive({ workspace: false, management: false, security: false });
const user = computed(() => session.value?.user);
const name = computed(() => user.value?.details?.name || user.value?.userName);
const initials = computed(() => name.value?.split(' ').map(part => part[0]).slice(0, 2).join('').toUpperCase());
const avatar = ref('');
let avatarGeneration = 0;
function clearAvatar() { if (avatar.value) URL.revokeObjectURL(avatar.value); avatar.value = ''; }
watch(() => [user.value?.linkedUniqueId, user.value?.details?.logo, user.value?.details?.version], async () => {
  const current = ++avatarGeneration;
  clearAvatar();
  if (!user.value?.details?.logo) return;
  try {
    const blob = await request('/profile/avatar', { responseType: 'blob' });
    if (current === avatarGeneration) avatar.value = URL.createObjectURL(blob);
  } catch { /* Initials remain visible when an image is unavailable. */ }
}, { immediate: true });
onBeforeUnmount(() => { avatarGeneration++; clearAvatar(); });
const title = computed(() => ({ overview: 'Overview', containers: 'Containers', tracking: 'Tracking', employees: 'Employees', categories: 'Business categories', locations: 'Location directory', consignees: 'Consignees', shippers: user.value?.userType === 3 ? 'My shipper details' : 'Shippers', account: 'My account' })[page.value]);
const sections = computed(() => [
  { id: 'workspace', label: 'Workspace', items: [{ id: 'overview', label: 'Overview', icon: LayoutDashboard }, ...([1, 2, 3].includes(user.value?.userType) ? [{ id: 'containers', label: 'Containers', icon: Boxes }, { id: 'tracking', label: 'Tracking', icon: Route }] : [])] },
  { id: 'management', label: 'Management', items: user.value?.userType === 1 ? [{ id: 'employees', label: 'Employees', icon: UsersRound }, { id: 'consignees', label: 'Consignees', icon: Building2 }, { id: 'shippers', label: 'Shippers', icon: Truck }, { id: 'categories', label: 'Business categories', icon: Tags }, { id: 'locations', label: 'Location directory', icon: MapPinned }] : [] },
  { id: 'security', label: 'Security', items: [2,3].includes(user.value?.userType) ? [] : [{ id: 'account', label: 'My account', icon: LockKeyhole }] }
]);
function openTracking(status = '', id = null, shipperId = null) { go('tracking'); trackingInitialStatus.value = status; trackingInitialId.value = id; trackingInitialShipper.value = shipperId; }
function go(id) { trackingInitialShipper.value = null; trackingInitialStatus.value = ''; trackingInitialId.value = null; containerCreateOnOpen.value = false; page.value = id; mobileNav.value = false; }
async function logout() { busy.value = true; error.value = ''; try { await signOut(); } catch (failure) { error.value = failure.message; } finally { busy.value = false; } }
</script>
<template>
  <div class="app-shell" @keydown.esc="mobileNav = false">
    <button v-if="mobileNav" class="sidebar-overlay" aria-label="Close navigation" @click="mobileNav = false"></button>
    <aside class="sidebar" :class="{ open: mobileNav }" aria-label="Main navigation">
      <button class="brand sidebar-brand" @click="go('overview')"><span class="brand-mark"><Boxes :size="24" /></span><span>kodiak<span class="brand-dot">.</span></span></button>
      <div class="workspace-label"><span class="live-dot"></span> OPERATIONS WORKSPACE</div>
      <button class="icon-btn sidebar-close" aria-label="Close navigation" @click="mobileNav = false"><X :size="20" /></button>
      <section v-for="section in sections.filter(item => item.items.length)" :key="section.id" class="navigation-section">
        <button class="section-toggle" :aria-expanded="expanded[section.id]" :aria-controls="'nav-' + section.id" @click="expanded[section.id] = !expanded[section.id]">{{ section.label }}<ChevronDown :size="14" :class="{ collapsed: !expanded[section.id] }" /></button>
        <nav v-show="expanded[section.id]" :id="'nav-' + section.id" :aria-label="section.label"><button v-for="item in section.items" :key="item.id" :class="{ active: page === item.id }" :aria-current="page === item.id ? 'page' : undefined" @click="go(item.id)"><component :is="item.icon" :size="18" /><span>{{ item.label }}</span></button><p v-if="!section.items.length" class="nav-empty">Administrator access required</p></nav>
      </section>
      <div class="sidebar-bottom"><button class="profile" @click="logout" :disabled="busy" aria-label="Sign out"><span class="avatar">{{ initials }}</span><span><strong>{{ name }}</strong><small>{{ user.userType === 1 ? 'Kodiak administrator' : 'Kodiak workspace' }}</small></span><LogOut :size="17" /></button></div>
    </aside>
    <div class="main-shell">
      <header class="topbar"><div class="breadcrumb"><button class="mobile-menu icon-btn" @click="mobileNav = true" aria-label="Open navigation" :aria-expanded="mobileNav"><Menu :size="22" /></button><span>Workspace</span><ChevronRight :size="14" /><strong>{{ title }}</strong></div><div class="topbar-actions"><slot name="theme" /><span class="demo-tag"><span></span> Operations workspace</span><span class="topbar-divider"></span><button class="top-avatar" @click="[2,3].includes(user.userType) ? companyProfile.open() : go('account')" :aria-label="[2,3].includes(user.userType) ? 'Edit my company profile' : 'My account'" :title="[2,3].includes(user.userType) ? 'Edit my company profile' : 'My account'"><img v-if="avatar" :src="avatar" alt="" :class="{ 'company-avatar-image': [2,3].includes(user.userType) }" @error="clearAvatar"/><span v-else>{{ initials }}</span></button><button class="icon-btn" @click="logout" :disabled="busy" aria-label="Sign out"><LogOut :size="18" /></button></div></header>
      <main class="application-main"><p v-if="error" class="form-error" role="alert">{{ error }}</p>
        <OverviewView v-if="page === 'overview' && [1,2,3].includes(user.userType)" @register="go('containers'); containerCreateOnOpen = true" @tracking="openTracking($event)" @journey="openTracking('',$event)" @shipper="openTracking('',null,$event)" />
        <EmployeesView v-else-if="page === 'employees' && user.userType === 1" />
        <ContainersView v-else-if="page === 'containers' && [1, 2, 3].includes(user.userType)" :create-on-open="containerCreateOnOpen" />
        <TrackingView v-else-if="page === 'tracking' && [1, 2, 3].includes(user.userType)" :initial-status="trackingInitialStatus" :initial-container-id="trackingInitialId" :initial-shipper-id="trackingInitialShipper" @register="go('containers'); containerCreateOnOpen = true" />
        <LocationDirectoryView v-else-if="page === 'locations' && user.userType === 1" />
        <ShippersView v-else-if="page === 'shippers' && user.userType === 1" />
        <ConsigneesView v-else-if="page === 'consignees' && user.userType === 1" />
        <BusinessCategoriesView v-else-if="page === 'categories' && user.userType === 1" />
        <template v-else><div class="page-heading"><div><p class="eyebrow">{{ page === 'account' ? 'SECURITY' : 'YOUR WORKSPACE' }}</p><h1>{{ page === 'account' ? 'My account' : 'Welcome, ' + name + '.' }}</h1><p>Your connected container operations workspace.</p></div></div><section class="panel account-panel"><div class="success-icon"><CheckCircle2 :size="25" /></div><h2>Account connected</h2><dl><div><dt>Username</dt><dd>{{ user.userName }}</dd></div><div><dt>Linked code</dt><dd>{{ user.linkedUniqueCode }}</dd></div><div><dt>Access</dt><dd><ShieldCheck :size="14" />{{ user.userType === 1 ? 'Administrator' : 'Active account' }}</dd></div></dl><button v-if="page === 'overview' && user.userType === 1" class="btn primary overview-action" @click="go('employees')"><UsersRound :size="16" />Manage employees</button></section></template>
      </main>
    </div>
    <CompanyProfileDialog v-if="[2,3].includes(user.userType)" ref="companyProfile" />
  </div>
</template>

<style scoped>
.top-avatar{overflow:hidden;padding:0;flex-shrink:0}.top-avatar img{display:block;width:100%;height:100%;object-fit:cover}.top-avatar img.company-avatar-image{object-fit:contain;padding:3px;background:#fff}
</style>

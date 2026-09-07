<script setup lang="ts">
defineProps<{ open?: boolean }>();
const links = [
  { label: 'Dashboard', to: '/' },
  { label: 'Profile', to: '/profile' }, { label: 'MFA security', to: '/security/mfa' }, { label: 'Sessions', to: '/security/sessions' }, { label: 'External logins', to: '/security/external-logins' }, { label: 'Authorization', to: '/admin/authorization' },
];
</script>
<template>
  <aside class="app-sidebar" :class="{ 'app-sidebar--open': open }" aria-label="Primary navigation">
    <RouterLink to="/" class="brand"><span class="brand-mark">D</span><span>V080ShowcaseVerified</span></RouterLink>
    <nav><RouterLink v-for="link in links" :key="link.to" :to="link.to" class="nav-link" active-class="nav-link--active" v-text="link.label"></RouterLink></nav>
    <div class="sidebar-footer"><span class="sidebar-caption">Built with Dotisan</span></div>
  </aside>
</template>
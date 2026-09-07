<script setup lang="ts">
import { ref } from 'vue';
import UiButton from './ui/Button.vue';
import { logout } from '../dotisan/services';
const menuOpen = ref(false);
async function signOut() { await logout(); window.location.assign('/auth/login'); }
</script>
<template>
  <header class="app-header">
    <div><p class="header-kicker">Workspace</p><h1>GeneratedAppintegrationsCheck</h1></div>
    <div class="user-menu"><UiButton variant="ghost" @click="menuOpen = !menuOpen">Account ▾</UiButton><div v-if="menuOpen" class="user-menu__panel"><RouterLink to="/profile">Profile</RouterLink><UiButton variant="ghost" @click="signOut">Sign out</UiButton></div></div>
  </header>
</template>
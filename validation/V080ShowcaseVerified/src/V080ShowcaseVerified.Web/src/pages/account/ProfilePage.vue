<script setup lang="ts">
import { onMounted, ref } from 'vue'; import UiCard from '../../components/ui/Card.vue'; import { me } from '../../dotisan/services'; const profile = ref<{ id: string; email: string }>(); const error = ref(''); onMounted(async () => { try { profile.value = await me(); } catch { error.value = 'Could not load your profile.'; } });
</script>
<template>
<section class="page-stack"><div class="page-heading"><div><p class="eyebrow">Account</p><h2>Your profile</h2></div></div><UiCard><p v-if="error" class="form-error" role="alert" v-text="error"></p><dl v-else-if="profile" class="profile-list"><div><dt>Email</dt><dd v-text="profile.email"></dd></div><div><dt>User ID</dt><dd class="mono" v-text="profile.id"></dd></div></dl><p v-else class="muted">Loading profile...</p></UiCard></section>
</template>
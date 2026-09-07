<script setup lang="ts">
import { onMounted, ref } from 'vue';
import UiCard from '../components/ui/Card.vue';
const notifications = ref<Array<{ id: string; title: string; body: string; isRead: boolean }>>([]);
onMounted(async () => { const response = await fetch('/api/notifications'); if (response.ok) notifications.value = (await response.json()).notifications; });
async function markRead(id: string) { await fetch(`/api/notifications/${id}/read`, { method: 'POST' }); const item = notifications.value.find(value => value.id === id); if (item) item.isRead = true; }
</script>
<template><section class="page-stack"><div class="page-heading"><div><p class="eyebrow">Inbox</p><h2>Notifications</h2></div></div><UiCard v-for="item in notifications" :key="item.id" :class="{ unread: !item.isRead }"><h3>{{ item.title }}</h3><p>{{ item.body }}</p><button v-if="!item.isRead" @click="markRead(item.id)">Mark read</button></UiCard><p v-if="notifications.length === 0" class="muted">You are all caught up.</p></section></template>
<script setup lang="ts">
import { onMounted, ref } from 'vue';
const deliveries = ref<Array<{ id: string; eventType: string; endpoint: string; status: string; attempts: number }>>([]);
onMounted(async () => { const response = await fetch('/api/webhooks/deliveries'); if (response.ok) deliveries.value = await response.json(); });
async function replay(id: string) { await fetch(`/api/webhooks/deliveries/${id}/replay`, { method: 'POST' }); }
</script>
<template><section class="page-stack"><div class="page-heading"><div><p class="eyebrow">Integrations</p><h2>Webhook deliveries</h2></div></div><table><thead><tr><th>Event</th><th>Endpoint</th><th>Status</th><th>Attempts</th><th></th></tr></thead><tbody><tr v-for="delivery in deliveries" :key="delivery.id"><td>{{ delivery.eventType }}</td><td>{{ delivery.endpoint }}</td><td>{{ delivery.status }}</td><td>{{ delivery.attempts }}</td><td><button @click="replay(delivery.id)">Replay</button></td></tr></tbody></table><p v-if="deliveries.length === 0" class="muted">No webhook deliveries yet.</p></section></template>
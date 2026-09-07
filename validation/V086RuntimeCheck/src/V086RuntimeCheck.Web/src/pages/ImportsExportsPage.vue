<script setup lang="ts">
import { ref } from 'vue';
const file = ref<File>(); const message = ref('');
async function importFile() { if (!file.value) return; const body = new FormData(); body.append('file', file.value); const response = await fetch('/api/data/imports', { method: 'POST', body }); message.value = response.ok ? 'Import queued.' : 'Import failed.'; }
</script>
<template><section class="page-stack"><div class="page-heading"><div><p class="eyebrow">Data</p><h2>Imports and exports</h2></div></div><form @submit.prevent="importFile"><input type="file" accept=".csv,.json" @change="file = ($event.target as HTMLInputElement).files?.[0]" /><button type="submit">Queue import</button></form><a href="/api/data/exports/csv">Download CSV export</a><p v-if="message" role="status">{{ message }}</p></section></template>
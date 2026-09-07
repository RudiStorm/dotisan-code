<script setup lang="ts">
import UiBadge from '../components/ui/Badge.vue';
import UiCard from '../components/ui/Card.vue';
</script>
<template>
  <section class="page-stack">
    <div class="page-heading"><div><p class="eyebrow">Overview</p><h2>Welcome to CiApiCheck2</h2><p class="page-lede">Your application workspace is ready. Start by adding your first feature.</p></div><UiBadge>Ready</UiBadge></div>
    <div class="dashboard-grid"><UiCard><template #header><h3>Application status</h3></template><p class="metric">Online</p><p class="muted">The API and Vue portal are connected.</p></UiCard><UiCard><template #header><h3>Next step</h3></template><p class="metric">Build a feature</p><p class="muted">Use <code>dotisan make:resource</code> to create your first vertical slice.</p></UiCard></div>
  </section>
</template>
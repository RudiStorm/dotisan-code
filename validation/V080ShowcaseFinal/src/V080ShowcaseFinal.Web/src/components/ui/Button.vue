<script setup lang="ts">
withDefaults(defineProps<{ variant?: 'default' | 'outline' | 'ghost' | 'destructive'; type?: 'button' | 'submit' | 'reset'; disabled?: boolean }>(), { variant: 'default', type: 'button' });
</script>
<template><button :type="type" :disabled="disabled" class="ui-button" :class="`ui-button--${variant}`"><slot /></button></template>
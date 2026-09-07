import { createApp } from 'vue';
import { createPinia } from 'pinia';
import { VueQueryPlugin, QueryClient } from '@tanstack/vue-query';
import router from './routes';
import App from './App.vue';
import './style.css';

const queryClient = new QueryClient();
createApp(App).use(createPinia()).use(VueQueryPlugin, { queryClient }).use(router).mount('#app');
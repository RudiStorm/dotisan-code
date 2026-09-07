import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router';
import PortalLayout from '../layouts/PortalLayout.vue';
import DashboardPage from '../pages/DashboardPage.vue';




const routes: RouteRecordRaw[] = [
  { path: '/', component: PortalLayout, children: [{ path: '', component: DashboardPage }] }
];
// DOTISAN:ROUTES
export default createRouter({ history: createWebHistory(), routes });
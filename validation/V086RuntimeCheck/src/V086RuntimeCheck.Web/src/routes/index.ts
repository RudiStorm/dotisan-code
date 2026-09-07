import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router';
import { me } from '../dotisan/services';
import PortalLayout from '../layouts/PortalLayout.vue';
import AuthLayout from '../layouts/AuthLayout.vue';
import DashboardPage from '../pages/DashboardPage.vue';
import LoginPage from '../pages/auth/LoginPage.vue';
import RegisterPage from '../pages/auth/RegisterPage.vue';
import ForgotPasswordPage from '../pages/auth/ForgotPasswordPage.vue';
import EmailConfirmationPage from '../pages/auth/EmailConfirmationPage.vue';
import MfaChallengePage from '../pages/auth/MfaChallengePage.vue';
import ProfilePage from '../pages/account/ProfilePage.vue';
import MfaPage from '../pages/account/MfaPage.vue';
import SessionsPage from '../pages/account/SessionsPage.vue';
import ExternalLoginsPage from '../pages/account/ExternalLoginsPage.vue';
import AuthorizationPage from '../pages/admin/AuthorizationPage.vue';
import NotificationsPage from '../pages/NotificationsPage.vue';
import ImportsExportsPage from '../pages/ImportsExportsPage.vue';
import WebhooksPage from '../pages/WebhooksPage.vue';

const routes: RouteRecordRaw[] = [
  { path: '/', component: PortalLayout, meta: { requiresAuth: true }, children: [{ path: '', component: DashboardPage }, { path: 'notifications', component: NotificationsPage }, { path: 'data', component: ImportsExportsPage }, { path: 'webhooks', component: WebhooksPage }, { path: 'profile', component: ProfilePage }, { path: 'security/mfa', component: MfaPage }, { path: 'security/sessions', component: SessionsPage }, { path: 'security/external-logins', component: ExternalLoginsPage }, { path: 'admin/authorization', component: AuthorizationPage, meta: { requiresPermission: 'authorization.manage' } }] },
  { path: '/auth', component: AuthLayout, children: [{ path: 'login', name: 'login', component: LoginPage }, { path: 'register', name: 'register', component: RegisterPage }, { path: 'forgot-password', component: ForgotPasswordPage }, { path: 'confirm-email', component: EmailConfirmationPage }, { path: 'mfa-challenge', component: MfaChallengePage }] }
];
// DOTISAN:ROUTES
const router = createRouter({ history: createWebHistory(), routes });
router.beforeEach(async (to) => {
  if (!to.meta.requiresAuth && !to.meta.requiresPermission) return true;
  try { await me(); return true; } catch { return { name: 'login', query: { redirect: to.fullPath } }; }
});
export default router;
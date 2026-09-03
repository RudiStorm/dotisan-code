import { expect, test } from '@playwright/test';

test('registration shows a friendly confirmation waiting state', async ({ page }) => {
  await page.route('**/api/account/antiforgery', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ token: 'browser-test-token' }) }));
  await page.route('**/api/account/register', route => route.fulfill({ status: 200, contentType: 'application/json', body: '{}' }));
  await page.goto('/auth/register');
  await expect(page.getByRole('heading', { name: 'Create account' })).toBeVisible();
  await page.getByLabel('Email').fill(`browser-${Date.now()}@example.com`);
  await page.getByLabel('Password').fill('Password1!');
  await page.getByRole('button', { name: 'Create account' }).click();

  await expect(page.getByText('Check your email for a confirmation link.')).toBeVisible();
  await expect(page.getByText('Request failed')).not.toBeVisible();
});

test('captured confirmation link completes email confirmation', async ({ page }) => {
  await page.route('**/api/account/antiforgery', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ token: 'browser-test-token' }) }));
  await page.route('**/api/account/email-confirmation/confirm', route => route.fulfill({ status: 204 }));
  await page.goto(process.env.DOTISAN_CONFIRMATION_URL ?? '/auth/confirm-email?email=browser%40example.com&token=browser-test-token');
  await expect(page.getByText('Your email has been confirmed. You can sign in.')).toBeVisible();
});

import { test, expect } from '@playwright/test';

const publisherName = process.env.PUBLISHER_DISPLAY_NAME ?? 'Alice';

test('publisher lands on the rules list with their name', async ({ page }) => {
  await page.goto('/');

  await expect(page).toHaveURL(/\/rules$/);
  await expect(page).toHaveTitle('Rules · eShopAdmin');
  await expect(page.getByRole('heading', { name: 'Rules', level: 1 })).toBeVisible();
  await expect(page.getByRole('banner')).toContainText(publisherName);
  await expect(page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Rules' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Audit log' })).toBeVisible();
});

test('sign out returns to the sign-in page', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('banner').getByRole('button', { name: 'Sign out' }).click();

  await expect(page.getByRole('heading', { name: 'Login' })).toBeVisible();
});

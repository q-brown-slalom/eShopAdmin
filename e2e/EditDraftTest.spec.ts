import { test, expect } from '@playwright/test';

// Where Catalog.API listens: the same default as the AppHost's catalog-url parameter.
const CATALOG_URL = process.env.CATALOG_URL ?? 'http://localhost:5222';

test('schedule a grace period draft', async ({ page }) => {
  await page.goto('/rules/ORD-001');
  await page.getByRole('link', { name: 'New draft' }).click();

  await expect(page).toHaveURL(/\/rules\/ORD-001\/drafts\/new$/);
  await expect(page).toHaveTitle('New draft · ORD-001 Grace period · eShopAdmin');

  // 61 is out of range. The API says so, and the message sits next to the input.
  const minutes = page.getByLabel('Minutes');
  await minutes.fill('61');
  await page.getByRole('button', { name: 'Save draft' }).click();

  await expect(minutes).toHaveAccessibleDescription(/must be between 1 and 60/);
  await expect(minutes).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByRole('alert')).toBeFocused();
  await expect(page).toHaveURL(/\/rules\/ORD-001\/drafts\/new$/);
  await expect(minutes).toHaveValue('61');

  // Fix it and schedule it for tomorrow at 09:00 UTC.
  const tomorrow = new Date();
  tomorrow.setUTCDate(tomorrow.getUTCDate() + 1);
  const day = tomorrow.toISOString().slice(0, 10);
  await minutes.fill('10');
  await page.getByLabel('Starts (UTC)').fill(`${day}T09:00`);
  await page.getByRole('button', { name: 'Save draft' }).click();

  await expect(page).toHaveURL(/\/rules\/ORD-001\?status=/);
  const saved = page.getByRole('status').filter({ hasText: /Draft v\d+ saved\./ });
  await expect(saved).toBeVisible();
  const version = /Draft v(\d+) saved\./.exec((await saved.textContent()) ?? '')![1];

  // The new version is the Draft row, with the value and the effective date.
  const row = page.getByRole('row').filter({ has: page.getByRole('rowheader', { name: `v${version}`, exact: true }) });
  await expect(row).toHaveCount(1);
  await expect(row).toContainText('Draft');
  await expect(row).toContainText('10 minutes');
  await expect(row).toContainText(`${day} 09:00`);
});

test('price adjustment rows can be added and removed', async ({ page, request }) => {
  await page.goto('/rules/PRC-001');
  await page.getByRole('link', { name: 'New draft' }).click();
  await expect(page).toHaveURL(/\/rules\/PRC-001\/drafts\/new$/);

  const entries = page.getByRole('group', { name: /^Entry \d+$/ });
  await expect(entries).toHaveCount(0);

  await page.getByRole('button', { name: 'Add entry' }).click();
  await expect(entries).toHaveCount(1);
  await page.getByLabel('Value').fill('11');

  await page.getByRole('button', { name: 'Add entry' }).click();
  await expect(entries).toHaveCount(2);
  // Adding a row keeps what was typed in the first one.
  await expect(page.getByLabel('Value').first()).toHaveValue('11');
  await page.getByLabel('Value').last().fill('22');

  await page.getByRole('button', { name: 'Remove entry 1' }).click();

  // The first row is the one that went: the second row's value is now in the only row left.
  await expect(entries).toHaveCount(1);
  await expect(page.getByLabel('Value')).toHaveValue('22');

  // The Type picker lists the catalog's types by name, in the catalog's order.
  const response = await request.get(`${CATALOG_URL}/api/catalog/catalogtypes?api-version=1.0`);
  expect(response.ok()).toBeTruthy();
  const names = ((await response.json()) as { type: string }[]).map((t) => t.type);
  expect(names.length).toBeGreaterThan(0);

  const options = await page.getByLabel('Target type').locator('option').allTextContents();
  expect(options.slice(1)).toEqual(names);
});

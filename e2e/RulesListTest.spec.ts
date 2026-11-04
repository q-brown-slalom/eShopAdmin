import { test, expect } from '@playwright/test';
import { EDITOR_STATE } from '../playwright.config';
import { asPublisher, createGracePeriodDraft } from './rules';

test('lists the five rule types', async ({ page }) => {
  await page.goto('/rules');

  await expect(page).toHaveTitle('Rules · eShopAdmin');
  await expect(page.getByRole('heading', { name: 'Rules', level: 1 })).toBeVisible();

  const table = page.getByRole('table', { name: /Business rules/ });
  for (const id of ['PRC-001', 'PRM-001', 'SHP-001', 'ORD-001', 'ORD-013']) {
    await expect(table.getByRole('row', { name: new RegExp(id) })).toHaveCount(1);
  }

  // Columns after the row header: Changes prices, Current value, Scheduled, Drafts.
  await expect(table.getByRole('row', { name: /ORD-001/ }).locator('td').first()).toHaveText('No');
  await expect(table.getByRole('row', { name: /SHP-001/ }).locator('td').first()).toHaveText('Yes');
});

test('the rule link opens the history page', async ({ page }) => {
  await page.goto('/rules');
  await page.getByRole('link', { name: /^ORD-001 / }).click();

  await expect(page).toHaveURL(/\/rules\/ORD-001$/);
  await expect(page.getByRole('heading', { level: 1 })).toHaveCount(1);
  await expect(page.getByRole('link', { name: 'New draft' })).toBeVisible();
});

test('an unknown rule type is a 404 page', async ({ page }) => {
  const response = await page.goto('/rules/NOPE-999');

  expect(response?.status()).toBe(404);
  await expect(page.getByRole('heading', { name: 'Page not found', level: 1 })).toBeVisible();
});

test.describe('as an editor', () => {
  test.use({ storageState: EDITOR_STATE });

  // Without a draft there is no Publish link to hide, so the check below would pass whatever the page did.
  test.beforeAll(async ({ browser }, testInfo) => {
    await asPublisher(browser, testInfo, (publisher) => createGracePeriodDraft(publisher, 13));
  });

  test('editor sees history without Publish', async ({ page }) => {
    await page.goto('/rules/ORD-001');

    await expect(page.getByRole('table', { name: /Versions of ORD-001/ })).toBeVisible();
    // The editor can edit the draft, so the row is there, and Publish isn't.
    await expect(page.getByRole('link', { name: /^Edit draft version/ }).first()).toBeVisible();
    await expect(page.getByRole('link', { name: /^Publish/ })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^Publish/ })).toHaveCount(0);
  });
});

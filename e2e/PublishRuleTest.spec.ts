import { test, expect } from '@playwright/test';
import { EDITOR_STATE } from '../playwright.config';
import { asPublisher, createGracePeriodDraft, openPublishPage, restoreGracePeriodDefault } from './rules';

// Specs publish ORD-001 only, so the price guard never fires here, and they put the built-in default back at the end.
test.afterAll(async ({ browser }, testInfo) => {
  await asPublisher(browser, testInfo, restoreGracePeriodDefault);
});

test('publish a scheduled grace period', async ({ page }) => {
  const version = await createGracePeriodDraft(page, 10);
  await openPublishPage(page, version);

  await expect(page).toHaveURL(/\/rules\/ORD-001\/versions\/[0-9a-f-]{36}\/publish$/);
  await expect(page).toHaveTitle(`Publish v${version} · ORD-001 Grace period · eShopAdmin`);

  // The comparison has a Current and a New column, and the new value is this draft's.
  const comparison = page.getByRole('table', { name: /Current and new settings for ORD-001/ });
  await expect(comparison.getByRole('columnheader', { name: 'Current' })).toBeVisible();
  await expect(comparison.getByRole('columnheader', { name: 'New' })).toBeVisible();
  await expect(comparison.getByRole('row', { name: /Value/ }).getByRole('cell').last()).toHaveText('10 minutes');

  await page.getByRole('button', { name: 'Publish', exact: true }).click();

  await expect(page).toHaveURL(/\/rules\/ORD-001\?status=/);
  await expect(page.getByRole('status').filter({ hasText: /^Published\. Rules snapshot \d+\./ })).toBeVisible();

  const row = page.getByRole('row').filter({ has: page.getByRole('rowheader', { name: `v${version}`, exact: true }) });
  await expect(row).toContainText('Published');
});

test('double submit shows a clear conflict', async ({ page, context }) => {
  const version = await createGracePeriodDraft(page, 11);
  await openPublishPage(page, version);

  // The same form, still open in a second tab, as when the first submit was slow and the user pressed Publish again.
  const second = await context.newPage();
  await second.goto(page.url());
  await expect(second.getByRole('button', { name: 'Publish', exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Publish', exact: true }).click();
  await expect(page).toHaveURL(/\/rules\/ORD-001\?status=/);

  await second.getByRole('button', { name: 'Publish', exact: true }).click();
  await expect(second.getByRole('alert').getByText(/^This version can't be changed any more/)).toBeVisible();
  await expect(second.getByRole('alert')).toBeFocused();
  await expect(second.getByRole('button', { name: /^Publish/ })).toHaveCount(0);
});

test.describe('as an editor', () => {
  test.use({ storageState: EDITOR_STATE });

  let publishPath: string;

  // An existing draft, so the editor is turned away from a page that would otherwise work.
  test.beforeAll(async ({ browser }, testInfo) => {
    publishPath = await asPublisher(browser, testInfo, async (publisher) => {
      const version = await createGracePeriodDraft(publisher, 12);
      await publisher.goto('/rules/ORD-001');
      const href = await publisher.getByRole('link', { name: `Publish version ${version}` }).getAttribute('href');
      expect(href).toMatch(/^rules\/ORD-001\/versions\/[0-9a-f-]{36}\/publish$/);
      return `/${href}`;
    });
  });

  test('editor cannot publish', async ({ page }) => {
    await page.goto(publishPath);

    await expect(page).toHaveURL(/\/access-denied(\?|$)/);
    await expect(page.getByRole('heading', { name: 'Access denied', level: 1 })).toBeVisible();
    await expect(page.getByText(
      "You don't have access to eShopAdmin. Ask an administrator for the RulesEditor or RulesPublisher role.")).toBeVisible();
    await expect(page.getByRole('button', { name: /^Publish/ })).toHaveCount(0);
  });
});

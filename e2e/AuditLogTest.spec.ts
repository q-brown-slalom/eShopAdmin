import { test, expect } from '@playwright/test';
import { EDITOR_STATE } from '../playwright.config';
import { asPublisher, createGracePeriodDraft, openPublishPage, restoreGracePeriodDefault } from './rules';

// Its own draft and publish of ORD-001, so this spec doesn't depend on PublishRuleTest having run. ORD-001 never trips
// the price guard, and the built-in default goes back at the end (P3-D10).
test.beforeAll(async ({ browser }, testInfo) => {
  await asPublisher(browser, testInfo, async (publisher) => {
    const version = await createGracePeriodDraft(publisher, 14);
    await openPublishPage(publisher, version);
    await publisher.getByRole('button', { name: 'Publish', exact: true }).click();
    await expect(publisher.getByRole('status').filter({ hasText: /^Published\./ })).toBeVisible();
  });
});

test.afterAll(async ({ browser }, testInfo) => {
  await asPublisher(browser, testInfo, restoreGracePeriodDefault);
});

test('audit shows the publish by me', async ({ page }) => {
  await page.goto('/audit');

  await expect(page).toHaveTitle('Audit log · eShopAdmin');
  await expect(page.getByRole('heading', { name: 'Audit log', level: 1 })).toBeVisible();

  const table = page.getByRole('table', { name: 'Rule changes, newest first' });
  for (const name of ['When (UTC)', 'Action', 'Rule', 'Version id', 'By', 'Reason']) {
    await expect(table.getByRole('columnheader', { name })).toBeVisible();
  }

  // Newest first: the publish above is the first row, ahead of its own "Draft created" row.
  const first = table.getByRole('row').nth(1);
  await expect(first.getByRole('cell').first()).toHaveText('Published');
  await expect(first.getByRole('cell').nth(1)).toHaveText('ORD-001');
  await expect(first.getByRole('cell').nth(3)).toContainText(' (you)');

  // The details hold the before and after JSON.
  await first.getByText(/^Before and after for entry/).click();
  await expect(first.locator('pre').last()).toContainText('"status": "Published"');
});

test('the first page has no previous page', async ({ page }) => {
  await page.goto('/audit?page=abc');

  const pager = page.getByRole('navigation', { name: 'Audit pages' });
  await expect(pager).toContainText('Page 1 of');
  await expect(pager.getByRole('link', { name: 'Previous' })).toHaveCount(0);
  await expect(pager.getByText('Previous')).toHaveAttribute('aria-disabled', 'true');

  // Only when the log is long enough to have a second page.
  const next = pager.getByRole('link', { name: 'Next' });
  if (await next.count() > 0) {
    await next.click();
    await expect(page).toHaveURL(/\/audit\?page=1$/);
    await expect(page.getByRole('navigation', { name: 'Audit pages' }).getByRole('link', { name: 'Previous' })).toBeVisible();
  }
});

test('a page past the end offers the first page', async ({ page }) => {
  await page.goto('/audit?page=99999');

  await expect(page.getByRole('table')).toHaveCount(0);
  await expect(page.getByText('There are no entries on this page.')).toBeVisible();
  await page.getByRole('link', { name: 'Back to the first page' }).click();
  await expect(page.getByRole('table', { name: 'Rule changes, newest first' })).toBeVisible();
});

test.describe('as an editor', () => {
  test.use({ storageState: EDITOR_STATE });

  test('the audit log is open to editors, and the publish is not theirs', async ({ page }) => {
    await page.goto('/audit');

    const first = page.getByRole('table', { name: 'Rule changes, newest first' }).getByRole('row').nth(1);
    await expect(first.getByRole('cell').first()).toHaveText('Published');
    await expect(first.getByRole('cell').nth(3)).not.toContainText('(you)');
  });
});

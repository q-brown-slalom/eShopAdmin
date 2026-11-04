import { expect, type Browser, type Page, type TestInfo } from '@playwright/test';
import { PUBLISHER_STATE } from '../playwright.config';

/** Runs `work` on a page signed in as the publisher, for setup that doesn't belong to the test's own user. */
export async function asPublisher<T>(browser: Browser, testInfo: TestInfo, work: (page: Page) => Promise<T>): Promise<T> {
  const context = await browser.newContext({
    storageState: PUBLISHER_STATE,
    baseURL: testInfo.project.use.baseURL,
    ignoreHTTPSErrors: true,
  });
  try {
    return await work(await context.newPage());
  } finally {
    await context.close();
  }
}

/**
 * Saves an ORD-001 (grace period) draft that starts now, through the form, and returns its version number.
 * Without `minutes` the form keeps the value it starts with. `fromDefault` starts it from the built-in default instead
 * of the current value.
 */
export async function createGracePeriodDraft(page: Page, minutes?: number, fromDefault = false): Promise<number> {
  await page.goto(`/rules/ORD-001/drafts/new${fromDefault ? '?from=default' : ''}`);
  if (minutes !== undefined) {
    await page.getByLabel('Minutes').fill(String(minutes));
  }
  await page.getByRole('button', { name: 'Save draft' }).click();

  const saved = page.getByRole('status').filter({ hasText: /Draft v\d+ saved\./ });
  await expect(saved).toBeVisible();
  return Number(/Draft v(\d+) saved\./.exec((await saved.textContent()) ?? '')![1]);
}

/** From the ORD-001 history page, opens the Publish page of a draft. */
export async function openPublishPage(page: Page, version: number): Promise<void> {
  await page.goto('/rules/ORD-001');
  await page.getByRole('link', { name: `Publish version ${version}` }).click();
  await expect(page.getByRole('heading', { level: 1, name: `Publish Grace period v${version}` })).toBeVisible();
}

/** Puts the built-in grace period back by publishing a draft started from it (P3-D10). */
export async function restoreGracePeriodDefault(page: Page): Promise<void> {
  const version = await createGracePeriodDraft(page, undefined, true);
  await openPublishPage(page, version);
  await page.getByRole('button', { name: 'Publish', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: /^Published\./ })).toBeVisible();
}

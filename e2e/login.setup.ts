import { test as setup, expect } from '@playwright/test';
import { EDITOR_STATE, PUBLISHER_STATE } from '../playwright.config';

const users = [
  { role: 'publisher', username: process.env.PUBLISHER_USERNAME ?? 'alice', stateFile: PUBLISHER_STATE },
  { role: 'editor', username: process.env.EDITOR_USERNAME ?? 'bob', stateFile: EDITOR_STATE },
];

for (const user of users) {
  setup(`sign in as ${user.role}`, async ({ page }) => {
    const password = process.env.PASSWORD;
    expect(password, 'PASSWORD is not set').toBeTruthy();

    // eShopAdmin challenges the anonymous visitor, so Identity.API's login page opens.
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Login' })).toBeVisible();

    await page.getByPlaceholder('Username').fill(user.username);
    await page.getByPlaceholder('Password').fill(password!);
    await page.getByRole('button', { name: 'Login' }).click();

    await expect(page.getByRole('heading', { name: 'eShopAdmin', level: 1 })).toBeVisible();
    await page.context().storageState({ path: user.stateFile });
  });
}

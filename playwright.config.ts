import { defineConfig, devices } from '@playwright/test';
require('dotenv').config({ path: './.env', quiet: true });
import path from 'path';

export const PUBLISHER_STATE = path.join(__dirname, 'playwright/.auth/publisher.json');
export const EDITOR_STATE = path.join(__dirname, 'playwright/.auth/editor.json');

const baseURL = process.env.ADMIN_URL ?? 'https://localhost:7250';

/**
 * eShop (Identity.API and BusinessRules.API) must already be running: see README.md.
 * See https://playwright.dev/docs/test-configuration.
 */
export default defineConfig({
  testDir: './e2e',
  /* Specs publish shared rule versions, so they run serially. */
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1,
  reporter: [['html', { open: 'never' }]],
  use: {
    baseURL,
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
    ...devices['Desktop Chrome'],
  },

  projects: [
    {
      name: 'setup',
      testMatch: '**/*.setup.ts',
    },
    {
      name: 'publisher',
      testMatch: '**/*.spec.ts',
      testIgnore: '**/*.editor.spec.ts',
      dependencies: ['setup'],
      use: { storageState: PUBLISHER_STATE },
    },
    {
      name: 'editor',
      testMatch: '**/*.editor.spec.ts',
      dependencies: ['setup'],
      use: { storageState: EDITOR_STATE },
    },
  ],

  webServer: {
    command: 'dotnet run --project src/eShopAdmin.AppHost',
    url: baseURL,
    ignoreHTTPSErrors: true,
    reuseExistingServer: !process.env.CI,
    stderr: 'pipe',
    stdout: 'pipe',
    timeout: process.env.CI ? (5 * 60_000) : 60_000,
  },
});

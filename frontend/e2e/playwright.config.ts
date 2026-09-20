import { defineConfig, devices } from "@playwright/test";

import {
  apiBaseURL,
  baseURL,
  gatedApiBaseURL,
  gatedBaseURL,
  gatedFeatureFlag,
  offlineBaseURL,
  startServers,
  unreachableApiURL,
} from "./fixtures/targets";

const isCI = Boolean(process.env.CI);
const browserOnly = { testIgnore: "**/tests/smoke/**" };

export default defineConfig({
  testDir: "./tests",
  outputDir: "./test-results",
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 1 : 0,
  ...(isCI && { workers: 2 }),
  reporter: isCI
    ? [["list"], ["html", { open: "never" }], ["github"]]
    : [["list"], ["html", { open: "never" }]],
  timeout: 30_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL,
    trace: "on-first-retry",
    video: "retain-on-failure",
    screenshot: "on",
    locale: "en-IN",
    timezoneId: "Asia/Kolkata",
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
    { name: "firefox", use: { ...devices["Desktop Firefox"] }, ...browserOnly },
    { name: "webkit", use: { ...devices["Desktop Safari"] }, ...browserOnly },
    { name: "mobile-android", use: { ...devices["Pixel 10"] }, ...browserOnly },
    { name: "mobile-ios", use: { ...devices["iPhone 17"] }, ...browserOnly },
  ],
  ...(startServers && {
    webServer: [
      {
        command: "dotnet run --project ../../backend/Hosts/Api/Dewiride.Erp.Host.Api --no-build",
        url: `${apiBaseURL}/healthz/live`,
        reuseExistingServer: !isCI,
        timeout: 120_000,
      },
      {
        command: "pnpm --filter @dewiride/erp-web start",
        url: `${baseURL}/healthz`,
        reuseExistingServer: !isCI,
        timeout: 120_000,
        env: { API_INTERNAL_URL: apiBaseURL },
      },
      {
        command: "pnpm --filter @dewiride/erp-web exec next start -p 3001",
        url: `${offlineBaseURL}/healthz`,
        reuseExistingServer: !isCI,
        timeout: 120_000,
        env: { API_INTERNAL_URL: unreachableApiURL },
      },
      {
        command:
          "dotnet run --project ../../backend/Hosts/Api/Dewiride.Erp.Host.Api --no-build --no-launch-profile",
        url: `${gatedApiBaseURL}/healthz/live`,
        reuseExistingServer: !isCI,
        timeout: 120_000,
        env: {
          ASPNETCORE_ENVIRONMENT: "Development",
          ASPNETCORE_URLS: gatedApiBaseURL,
          APPCONFIG_ENDPOINT: "",
          feature_management__feature_flags__0__id: gatedFeatureFlag,
          feature_management__feature_flags__0__enabled: "false",
        },
      },
      {
        command: "pnpm --filter @dewiride/erp-web exec next start -p 3002",
        url: `${gatedBaseURL}/healthz`,
        reuseExistingServer: !isCI,
        timeout: 120_000,
        env: { API_INTERNAL_URL: gatedApiBaseURL },
      },
    ],
  }),
});

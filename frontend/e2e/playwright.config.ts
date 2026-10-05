import { defineConfig, devices } from "@playwright/test";

import {
  apiBaseURL,
  baseURL,
  gatedApiBaseURL,
  gatedBaseURL,
  gatedFeatureFlags,
  offlineBaseURL,
  startServers,
  unreachableApiURL,
} from "./fixtures/targets";

const isCI = Boolean(process.env.CI);
const browserOnly = { testIgnore: "**/tests/smoke/**" };
const runnerStartsServers = startServers && process.env.TEST_WORKER_INDEX === undefined;
const endToEndHost =
  "dotnet run --project ../../backend/Tests/EndToEnd/Dewiride.Erp.Testing.EndToEndHost --no-build --";
const testingEnvironment = { ASPNETCORE_ENVIRONMENT: "Testing" };
const webPorts = { primary: 3100, offline: 3101, gated: 3102 };

const portOf = (origin: string | undefined) => new URL(origin ?? "").port;
const webOrigin = (port: number) => `http://127.0.0.1:${port}`;
const nextStart = (port: number) => `pnpm --filter @dewiride/erp-web exec next start -H 127.0.0.1 -p ${port}`;

export default defineConfig({
  testDir: "./tests",
  outputDir: "./test-results",
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 1 : 0,
  ...(isCI && { workers: 2 }),
  reporter: isCI ? [["list"], ["blob"], ["github"]] : [["list"], ["html", { open: "never" }]],
  timeout: 30_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL,
    ignoreHTTPSErrors: true,
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
    { name: "tablet-ios", use: { ...devices["iPad (gen 11)"] }, ...browserOnly },
  ],
  ...(runnerStartsServers && {
    webServer: [
      {
        name: "api",
        command: `${endToEndHost} --port ${portOf(apiBaseURL)} --web-origin ${baseURL}`,
        url: `${apiBaseURL}/healthz/live`,
        reuseExistingServer: !isCI,
        timeout: 180_000,
        gracefulShutdown: { signal: "SIGTERM", timeout: 10_000 },
        env: testingEnvironment,
      },
      {
        name: "web",
        command: nextStart(webPorts.primary),
        url: `${webOrigin(webPorts.primary)}/healthz`,
        reuseExistingServer: !isCI,
        timeout: 120_000,
        env: { API_INTERNAL_URL: apiBaseURL },
      },
      {
        name: "web without an api",
        command: nextStart(webPorts.offline),
        url: `${webOrigin(webPorts.offline)}/healthz`,
        reuseExistingServer: !isCI,
        timeout: 120_000,
        env: { API_INTERNAL_URL: unreachableApiURL },
      },
      {
        name: "gated api",
        command: `${endToEndHost} --port ${portOf(gatedApiBaseURL)} --web-origin ${gatedBaseURL ?? ""}`,
        url: `${gatedApiBaseURL}/healthz/live`,
        reuseExistingServer: !isCI,
        timeout: 180_000,
        gracefulShutdown: { signal: "SIGTERM", timeout: 10_000 },
        env: {
          ...testingEnvironment,
          ...Object.fromEntries(
            gatedFeatureFlags.flatMap((flag, index) => [
              [`feature_management__feature_flags__${index}__id`, flag],
              [`feature_management__feature_flags__${index}__enabled`, "false"],
            ]),
          ),
        },
      },
      {
        name: "gated web",
        command: nextStart(webPorts.gated),
        url: `${webOrigin(webPorts.gated)}/healthz`,
        reuseExistingServer: !isCI,
        timeout: 120_000,
        env: { API_INTERNAL_URL: gatedApiBaseURL },
      },
      {
        name: "https",
        command: [
          "node servers/https-front.ts",
          `${portOf(baseURL)}=${webOrigin(webPorts.primary)}`,
          `${portOf(offlineBaseURL)}=${webOrigin(webPorts.offline)}`,
          `${portOf(gatedBaseURL)}=${webOrigin(webPorts.gated)}`,
        ].join(" "),
        url: `${baseURL}/healthz`,
        ignoreHTTPSErrors: true,
        reuseExistingServer: !isCI,
        timeout: 60_000,
      },
    ],
  }),
});

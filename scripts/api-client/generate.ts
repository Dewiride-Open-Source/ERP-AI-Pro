import { spawnSync } from "node:child_process";
import { existsSync, rmSync } from "node:fs";
import { join, posix, relative, sep } from "node:path";
import { parseArgs } from "node:util";

import { backendRoot, fail, generate, generatedRoot, lockFileName, logFileName, repoRoot } from "./lib/kiota.ts";

const snapshotProject = "Tests/Host/Dewiride.Erp.Host.Api.IntegrationTests/Dewiride.Erp.Host.Api.IntegrationTests.csproj";
const snapshotFilter = "*OpenApiSnapshotTests";
const testHostVariables = ["ERP_TEST_SQL_CONNECTION", "ERP_TEST_BLOB_EMULATOR_HOST"];

const { values } = parseArgs({
  options: {
    "skip-snapshot": { type: "boolean", default: false },
    help: { type: "boolean", default: false },
  },
});

if (values.help) {
  console.log("Usage: node scripts/api-client/generate.ts [--skip-snapshot]");
  process.exit(0);
}

if (!values["skip-snapshot"]) {
  const missing = testHostVariables.filter((name) => !process.env[name]);
  if (missing.length > 0) {
    fail(
      `${new Intl.ListFormat("en", { type: "conjunction" }).format(missing)} ${missing.length === 1 ? "is" : "are"} not set, so the API test host cannot start to refresh docs/openapi/erp.json. Set ${missing.length === 1 ? "it" : "them"} (see docs/guides/testing.md) or pass --skip-snapshot.`,
    );
  }

  console.log("▶ refreshing docs/openapi/erp.json from the API test host");
  const test = spawnSync("dotnet", ["test", "--project", snapshotProject, "--filter-class", snapshotFilter], {
    cwd: backendRoot,
    stdio: "inherit",
    env: { ...process.env, ERP_OPENAPI_SNAPSHOT: "update" },
  });
  if (test.status !== 0) fail(`the OpenAPI snapshot test exited with ${test.status}.`);
}

console.log(`▶ generating ${relative(repoRoot, generatedRoot).split(sep).join(posix.sep)}`);
generate(generatedRoot);
rmSync(join(generatedRoot, logFileName), { force: true });

if (!existsSync(join(generatedRoot, lockFileName))) {
  fail("kiota did not write kiota-lock.json, so the generated client is incomplete.");
}

console.log("✔ api client generated");

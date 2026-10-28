import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

import { imageNames, noticesFiles } from "../checks/lib/image-contents.ts";
import { repoRoot } from "../checks/lib/walk.ts";
import { committedNotices, writeNotices } from "./lib/images.ts";
import { differences } from "./lib/render.ts";

const committed = committedNotices(repoRoot);
const workspace = mkdtempSync(join(tmpdir(), "erp-notices-drift-"));
let problems: string[] = [];

try {
  const written = await writeNotices(repoRoot, workspace, imageNames);
  const expected = new Set(imageNames.flatMap((image) => noticesFiles.map((file) => `${image}/${file}`)));
  const present = existsSync(committed) ? readdirSync(committed, { recursive: true, withFileTypes: true }).filter((entry) => entry.isFile()).map((entry) => join(entry.parentPath, entry.name).slice(committed.length + 1).replaceAll("\\", "/")) : [];
  problems = [
    ...present.filter((path) => !expected.has(path)).map((path) => `infra/docker/notices/${path} is not a notices file of an image`),
    ...[...expected].flatMap((path) => {
      const target = join(committed, path);
      if (!existsSync(target)) return [`infra/docker/notices/${path} is missing`];
      return differences(readFileSync(target, "utf8"), readFileSync(join(workspace, path), "utf8")).map((line) => `infra/docker/notices/${path}: ${line}`);
    }),
  ];
  if (problems.length === 0) console.log(`notices ok: ${written.map(({ image, components }) => `${image} ${components} components`).join(", ")} match the pinned base images, the published hosts and the lockfile`);
} catch (error) {
  problems = [`notices: ${(error as Error).message}`];
} finally {
  rmSync(workspace, { recursive: true, force: true });
}

if (problems.length > 0) {
  console.error("The committed notices differ from what scripts/notices/generate.ts writes now. Run 'node scripts/notices/generate.ts' and commit the result:");
  for (const problem of problems) console.error(`  ${problem}`);
  process.exit(1);
}

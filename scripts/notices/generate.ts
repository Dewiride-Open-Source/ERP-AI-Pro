import { relative, resolve, sep } from "node:path";
import { parseArgs } from "node:util";

import { imageNames, isImageName } from "../checks/lib/image-contents.ts";
import { repoRoot } from "../checks/lib/walk.ts";
import { committedNotices, writeNotices } from "./lib/images.ts";

const usage = `Usage: node scripts/notices/generate.ts [--image <${imageNames.join("|")}>] [--output <folder>]

Writes THIRD-PARTY-NOTICES.txt and third-party-notices.json of each image into infra/docker/notices/<image>/ (or <folder>/<image>/).
Needs Docker (the pinned base images are read with docker create and docker cp, never run), the .NET SDK (the hosts are restored
in locked mode and published as api.Dockerfile publishes them), "pnpm install --frozen-lockfile" in frontend and network access to
Caddy's release on GitHub.`;

const { values, positionals } = parseArgs({
  allowPositionals: true,
  options: {
    image: { type: "string" },
    output: { type: "string" },
    help: { type: "boolean", default: false },
  },
});

if (values.help || positionals.length > 0 || (values.image !== undefined && !isImageName(values.image))) {
  console.log(usage);
  process.exit(values.help ? 0 : 64);
}

const images = values.image === undefined ? imageNames : imageNames.filter((image) => image === values.image);
const output = values.output === undefined ? committedNotices(repoRoot) : resolve(values.output);

try {
  for (const written of await writeNotices(repoRoot, output, images)) {
    const files = written.files.map(({ path, bytes }) => `${relative(repoRoot, path).split(sep).join("/")} (${bytes} bytes)`).join(", ");
    console.log(`notices ${written.image}: ${written.components} components, ${written.texts} licence and notice texts: ${files}`);
  }
} catch (error) {
  console.error(`notices: ${(error as Error).message}`);
  process.exit(1);
}

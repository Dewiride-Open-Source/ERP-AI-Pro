import { writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { parseArgs } from "node:util";

import { finalFileSystem, imageNames, isImageName, readSavedImage } from "../checks/lib/image-contents.ts";
import type { NoticesDocument } from "./lib/render.ts";
import { imageEnvironment, inventoryProblems, isInventoryEvidence, noticesPath, observedImage, spdxDocument } from "./lib/spdx.ts";

const usage = `Usage: node scripts/notices/sbom.ts <image reference> --image <${imageNames.join("|")}> --version <version> --output <file>

Reads the image through "docker save", checks that the third-party notices it carries list exactly the packages its dpkg or apk
database, its .NET dependency files, its npm package manifests and the Go build information of its Caddy binary name, and writes
an SPDX 2.3 JSON document of the image built from those notices.`;

const { positionals, values } = parseArgs({
  allowPositionals: true,
  options: {
    image: { type: "string" },
    version: { type: "string" },
    output: { type: "string" },
    help: { type: "boolean", default: false },
  },
});

const [reference] = positionals;
if (values.help || reference === undefined || positionals.length > 1 || !isImageName(values.image) || values.version === undefined || values.output === undefined) {
  console.log(usage);
  process.exit(values.help ? 0 : 64);
}

const image = values.image;
const version = values.version;
const output = resolve(values.output);

try {
  const archive = await readSavedImage(reference, isInventoryEvidence);
  const files = finalFileSystem(archive.layers);
  const labels = archive.config.Labels ?? {};
  const env = imageEnvironment(archive);
  const notices = files.get(noticesPath)?.content;
  const problems: string[] = [];
  if (notices === undefined) problems.push(`the image has no /${noticesPath}`);
  if (labels["org.opencontainers.image.version"] !== version) problems.push(`the image's org.opencontainers.image.version label is ${labels["org.opencontainers.image.version"] ?? "missing"}, not ${version}`);
  const licence = labels["org.opencontainers.image.licenses"];
  if (licence === undefined) problems.push("the image has no org.opencontainers.image.licenses label");
  const created = labels["org.opencontainers.image.created"] || archive.created;
  if (created === undefined) problems.push("the image records no creation time");
  const document = notices === undefined ? undefined : (JSON.parse(new TextDecoder().decode(notices)) as NoticesDocument);
  if (document !== undefined) problems.push(...inventoryProblems(image, document, observedImage(files, env)));
  if (problems.length > 0 || document === undefined || licence === undefined || created === undefined) {
    console.error(`sbom: ${reference} (${image}):`);
    for (const problem of problems) console.error(`  ${problem}`);
    process.exit(1);
  }
  const spdx = spdxDocument({ image, version, created, configDigest: archive.configDigest, licence, document });
  writeFileSync(output, `${JSON.stringify(spdx, null, 2)}\n`);
  console.log(`sbom ok: ${reference} (${image}) matches its notices; ${Object.keys(document.components).length} packages written to ${output}`);
} catch (error) {
  console.error(`sbom: ${reference}: ${(error as Error).message}`);
  process.exit(1);
}

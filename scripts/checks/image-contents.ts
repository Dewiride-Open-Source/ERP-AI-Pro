import { parseArgs } from "node:util";

import { imageContentsProblems, imageNames, isImageName, isRequiredFile, readSavedImage, repositoryCopies } from "./lib/image-contents.ts";
import { repoRoot } from "./lib/walk.ts";

const usage = `Usage: node scripts/checks/image-contents.ts <image reference> --image <${imageNames.join("|")}>

Reads every layer of the image through "docker save" and fails when any layer holds a file the images must never carry,
when /usr/share/doc/erp-ai-pro lacks the licence or the notices files of this checkout, or when a label is not the image's own.`;

const { positionals, values } = parseArgs({
  allowPositionals: true,
  options: {
    image: { type: "string" },
    help: { type: "boolean", default: false },
  },
});

const [reference] = positionals;
if (values.help || reference === undefined || positionals.length > 1 || !isImageName(values.image)) {
  console.log(usage);
  process.exit(values.help ? 0 : 64);
}

const image = values.image;
let problems: string[];
let summary: string;
try {
  const archive = await readSavedImage(reference, isRequiredFile);
  problems = imageContentsProblems(image, archive, repositoryCopies(repoRoot, image));
  summary = `${archive.layers.length} layers, ${archive.layers.reduce((total, layer) => total + layer.entries.length, 0)} entries`;
} catch (error) {
  console.error(`image contents: ${reference}: ${(error as Error).message}`);
  process.exit(1);
}

if (problems.length > 0) {
  console.error(`image contents of ${reference} (${image}):`);
  for (const problem of problems) console.error(`  ${problem}`);
  process.exit(1);
}

console.log(`image contents ok: ${reference} (${image}, ${summary}) holds no forbidden file, the licence and notices files of this checkout and only its own labels`);

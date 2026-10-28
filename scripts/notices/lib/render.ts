import { createHash } from "node:crypto";

import { documentationFolder, imageRepository, type ImageName, repositoryUrl } from "../../checks/lib/image-contents.ts";
import { type Component, type ComponentKind, type ImageInventory, noAssertion } from "./inventory.ts";
import { spdxExpression } from "./licence-texts.ts";

export type NoticesEntry = {
  readonly kind: ComponentKind;
  readonly name: string;
  readonly version: string;
  readonly licence: string;
  readonly spdx: string;
  readonly copyright: readonly string[];
  readonly texts: readonly number[];
  readonly notices: readonly number[];
  readonly inImage: readonly string[];
  readonly source: string;
  readonly supplier?: string;
  readonly detail?: string;
};

export type NoticesDocument = {
  readonly image: ImageName;
  readonly repository: string;
  readonly base: string;
  readonly system: string;
  readonly source: string;
  readonly copyright: string;
  readonly components: Readonly<Record<string, NoticesEntry>>;
  readonly texts: Readonly<Record<string, { readonly title: string; readonly sha256: string }>>;
};

const sectionTitles: Readonly<Record<ComponentKind, string>> = {
  runtime: "Runtimes and tools of the base image",
  nuget: "NuGet packages of the published .NET hosts",
  npm: "npm packages of the web app",
  golang: "Go modules built into Caddy",
  deb: "Operating system packages (dpkg)",
  apk: "Operating system packages (apk)",
};

function numbering(inventory: ImageInventory): Map<string, number> {
  const numbers = new Map<string, number>();
  for (const component of inventory.components) {
    for (const key of [...component.texts, ...component.notices]) if (!numbers.has(key)) numbers.set(key, numbers.size + 1);
  }
  return numbers;
}

export function noticesDocument(inventory: ImageInventory, copyright: string): NoticesDocument {
  const numbers = numbering(inventory);
  const numbered = (keys: readonly string[]) => keys.map((key) => numbers.get(key) as number);
  const components = Object.fromEntries(
    inventory.components.map((component): [string, NoticesEntry] => [
      component.purl,
      {
        kind: component.kind,
        name: component.name,
        version: component.version,
        licence: component.licence,
        spdx: component.licence === noAssertion ? noAssertion : (spdxExpression(component.licence) ?? noAssertion),
        copyright: component.copyright,
        texts: numbered(component.texts),
        notices: numbered(component.notices),
        inImage: component.inImage,
        source: component.source,
        ...(component.supplier === undefined ? {} : { supplier: component.supplier }),
        ...(component.detail === undefined ? {} : { detail: component.detail }),
      },
    ]),
  );
  const texts = Object.fromEntries(
    [...numbers].map(([key, number]) => {
      const text = inventory.texts.get(key);
      if (text === undefined) throw new Error(`a component refers to a text the inventory lacks (${key})`);
      return [String(number), { title: text.title, sha256: createHash("sha256").update(text.text).digest("hex") }];
    }),
  );
  return { image: inventory.image, repository: imageRepository(inventory.image), base: inventory.base, system: inventory.system, source: repositoryUrl, copyright, components, texts };
}

export function renderJson(document: NoticesDocument): string {
  return `${JSON.stringify(document, null, 2)}\n`;
}

export const listedDifferences = 5;

export function differences(committed: string, generated: string): string[] {
  if (committed === generated) return [];
  const left = committed.split("\n");
  const right = generated.split("\n");
  const found: string[] = [];
  const quoted = (line: string | undefined) => (line === undefined ? "the end of the file" : JSON.stringify(line.length > 120 ? `${line.slice(0, 120)}…` : line));
  for (let index = 0; index < Math.max(left.length, right.length) && found.length < listedDifferences; index += 1) {
    if (left[index] !== right[index]) found.push(`line ${index + 1} holds ${quoted(left[index])}, the generator writes ${quoted(right[index])}`);
  }
  return found;
}

const undeclaredLicence: Readonly<Record<ComponentKind, string>> = {
  runtime: "not declared; see its licence text",
  nuget: "not declared as an SPDX expression; see its licence text",
  npm: "not declared; see its licence text",
  golang: "not declared by the module, and its licence text matches no single SPDX licence; see the text",
  deb: "not declared in a form an SPDX expression can state; see its copyright file",
  apk: "not declared",
};

function entryLines(component: Component, numbers: ReadonlyMap<string, number>): string[] {
  const refer = (keys: readonly string[]) => keys.map((key) => `[${numbers.get(key)}]`).join(", ");
  return [
    `${component.name} ${component.version}`,
    `  Licence: ${component.licence === noAssertion ? undeclaredLicence[component.kind] : component.licence}`,
    ...component.copyright.map((line) => `  ${line}`),
    ...(component.texts.length === 0 ? [] : [`  Licence text: ${refer(component.texts)}`]),
    ...(component.notices.length === 0 ? [] : [`  Notices: ${refer(component.notices)}`]),
    ...(component.inImage.length === 0 ? [] : [`  In this image: ${component.inImage.join(", ")}`]),
    `  Source: ${component.source}`,
    ...(component.detail === undefined ? [] : [`  Note: ${component.detail}`]),
    `  Package URL: ${component.purl}`,
    "",
  ];
}

function underline(title: string, character: string): string[] {
  return [title, character.repeat(title.length), ""];
}

export function renderText(inventory: ImageInventory, copyright: string): string {
  const numbers = numbering(inventory);
  const repository = imageRepository(inventory.image);
  const header = [
    ...underline(`Third-party notices of the ERP-AI-Pro ${inventory.image} image`, "="),
    `This file describes ${repository},`,
    `built on ${inventory.base}`,
    `(${inventory.system}).`,
    "",
    `ERP-AI-Pro is free software. ${copyright}`,
    "It is licensed under the GNU Lesser General Public License version 3 only (LGPL-3.0-only),",
    `in /${documentationFolder}/COPYING.LESSER, which supplements the GNU General Public License version 3`,
    `in /${documentationFolder}/COPYING. Its corresponding source is the commit of the release tag this image`,
    `was built from, in ${repositoryUrl}.`,
    "",
    "The image also holds the third-party components below, each under its own licence. Every entry names",
    "the component's licence, its copyright notices and where its source or package is. A licence or notice",
    "text is printed once, at the end, and the entries refer to it by its number; a path names a file that",
    "this image itself holds.",
    "",
  ];
  const sections = (Object.keys(sectionTitles) as ComponentKind[]).flatMap((kind) => {
    const components = inventory.components.filter((component) => component.kind === kind);
    if (components.length === 0) return [];
    return [...underline(`${sectionTitles[kind]} (${components.length})`, "-"), ...components.flatMap((component) => entryLines(component, numbers))];
  });
  const texts = [...numbers].flatMap(([key, number]) => {
    const text = inventory.texts.get(key);
    if (text === undefined) throw new Error(`a component refers to a text the inventory lacks (${key})`);
    return [...underline(`[${number}] ${text.title}`, "-"), text.text, ""];
  });
  return `${[...header, ...sections, ...underline("Licence and notice texts", "="), ...texts].join("\n").trimEnd()}\n`;
}

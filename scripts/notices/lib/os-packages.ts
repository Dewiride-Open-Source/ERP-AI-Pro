import type { ImageFiles } from "./container-files.ts";
import { apkPurl, type Component, debPurl, noAssertion } from "./inventory.ts";
import { canonicalText, licenceIds, spdxExpression, type TextTable, textBase } from "./licence-texts.ts";

export type Paragraph = ReadonlyMap<string, string>;

export function controlParagraphs(text: string): Paragraph[] {
  const paragraphs: Paragraph[] = [];
  let current = new Map<string, string>();
  let field: string | undefined;
  for (const line of text.replace(/\r\n?/g, "\n").split("\n")) {
    if (line.startsWith("#")) continue;
    if (line.trim() === "") {
      if (current.size > 0) paragraphs.push(current);
      current = new Map();
      field = undefined;
    } else if (/^[ \t]/.test(line)) {
      if (field !== undefined) current.set(field, `${current.get(field) ?? ""}\n${line.slice(1)}`);
    } else {
      const colon = line.indexOf(":");
      if (colon <= 0) throw new Error(`a control file line is neither a field nor a continuation: ${line.slice(0, 80)}`);
      field = line.slice(0, colon).toLowerCase();
      current.set(field, line.slice(colon + 1).trim());
    }
  }
  if (current.size > 0) paragraphs.push(current);
  return paragraphs;
}

export type DebianPackage = { readonly name: string; readonly version: string; readonly architecture: string; readonly source: string; readonly sourceVersion: string; readonly maintainer: string };

export function installedDebianPackages(status: string): DebianPackage[] {
  return controlParagraphs(status).flatMap((paragraph) => {
    const name = paragraph.get("package");
    const version = paragraph.get("version");
    const state = paragraph.get("status");
    if (name === undefined || version === undefined) throw new Error("a dpkg status paragraph lacks Package or Version");
    if (state !== undefined && !/\binstalled$/.test(state)) return [];
    const source = /^(\S+)(?:\s+\((\S+)\))?$/.exec(paragraph.get("source") ?? name);
    if (source === null) throw new Error(`the Source field of ${name} is malformed`);
    return [{ name, version, architecture: paragraph.get("architecture") ?? "", source: source[1] as string, sourceVersion: source[2] ?? version, maintainer: paragraph.get("maintainer") ?? "" }];
  });
}

const debianLicenceNames: Readonly<Record<string, string>> = {
  "apache-2.0": "Apache-2.0",
  artistic: "Artistic-1.0-Perl",
  "artistic-2.0": "Artistic-2.0",
  "bsd-2-clause": "BSD-2-Clause",
  "bsd-3-clause": "BSD-3-Clause",
  "bsd-4-clause": "BSD-4-Clause",
  "cc0-1.0": "CC0-1.0",
  expat: "MIT",
  mit: "MIT",
  isc: "ISC",
  zlib: "Zlib",
  "mpl-1.1": "MPL-1.1",
  "mpl-2.0": "MPL-2.0",
  icu: "ICU",
  "gpl-1": "GPL-1.0-only",
  "gpl-1+": "GPL-1.0-or-later",
  "gpl-2": "GPL-2.0-only",
  "gpl-2+": "GPL-2.0-or-later",
  "gpl-3": "GPL-3.0-only",
  "gpl-3+": "GPL-3.0-or-later",
  "lgpl-2": "LGPL-2.0-only",
  "lgpl-2+": "LGPL-2.0-or-later",
  "lgpl-2.1": "LGPL-2.1-only",
  "lgpl-2.1+": "LGPL-2.1-or-later",
  "lgpl-3": "LGPL-3.0-only",
  "lgpl-3+": "LGPL-3.0-or-later",
  "gfdl-1.2": "GFDL-1.2-only",
  "gfdl-1.2+": "GFDL-1.2-or-later",
  "gfdl-1.3": "GFDL-1.3-only",
  "gfdl-1.3+": "GFDL-1.3-or-later",
};

const debianExceptions: Readonly<Record<string, string>> = {
  gcc: "GCC-exception-3.1",
  "gcc-exception": "GCC-exception-3.1",
  "gcc-exception-3.1": "GCC-exception-3.1",
};

function spdxOfDebianName(name: string): string | undefined {
  const words = name.trim().split(/\s+/);
  const output: string[] = [];
  for (let index = 0; index < words.length; index += 1) {
    const word = (words[index] as string).toLowerCase().replace(/,$/, "");
    if (word === "or" || word === "and") output.push(word.toUpperCase());
    else if (word === "with") {
      const end = words.findIndex((candidate, position) => position > index && candidate.toLowerCase() === "exception");
      const exception = debianExceptions[words.slice(index + 1, end === -1 ? index + 2 : end).join(" ").toLowerCase()];
      if (exception === undefined) return undefined;
      output.push("WITH", exception);
      index = end === -1 ? index + 1 : end;
    } else {
      const id = debianLicenceNames[word];
      if (id === undefined) return undefined;
      output.push(id);
    }
  }
  return spdxExpression(output.join(" "));
}

export type DebianCopyright = { readonly machineReadable: boolean; readonly licences: readonly string[]; readonly commonLicences: readonly string[] };

export function debianCopyright(text: string): DebianCopyright {
  const commonLicences = [...new Set([...text.matchAll(/\/usr\/share\/common-licenses\/([A-Za-z][A-Za-z0-9.+-]*[A-Za-z0-9+])/g)].map((match) => match[1] as string))].sort();
  if (!/^Format:\s*\S/m.test(text.split(/\n\s*\n/)[0] ?? "")) return { machineReadable: false, licences: [], commonLicences };
  const licences = new Set<string>();
  for (const paragraph of controlParagraphs(text)) {
    const licence = paragraph.get("license");
    if (licence === undefined || !paragraph.has("files")) continue;
    licences.add((licence.split("\n")[0] as string).trim());
  }
  return { machineReadable: true, licences: [...licences].sort(), commonLicences };
}

export function debianLicenceExpression(copyright: DebianCopyright | undefined): string {
  if (copyright === undefined || !copyright.machineReadable || copyright.licences.length === 0) return noAssertion;
  const ids = copyright.licences.map(spdxOfDebianName);
  if (ids.some((id) => id === undefined)) return noAssertion;
  const parts = [...new Set(ids as string[])].map((id) => (/\s(?:OR|AND)\s/.test(id) ? `(${id})` : id));
  return spdxExpression(parts.join(" AND ")) ?? noAssertion;
}

const commonLicenceTexts: Readonly<Record<string, string>> = {
  "Apache-2.0": "Apache-2.0",
  Artistic: "Artistic-1.0-Perl",
  "CC0-1.0": "CC0-1.0",
  GFDL: "GFDL-1.3",
  "GFDL-1.2": "GFDL-1.2",
  "GFDL-1.3": "GFDL-1.3",
  GPL: "GPL-3.0",
  "GPL-1": "GPL-1.0",
  "GPL-2": "GPL-2.0",
  "GPL-3": "GPL-3.0",
  LGPL: "LGPL-3.0",
  "LGPL-2": "LGPL-2.0",
  "LGPL-2.1": "LGPL-2.1",
  "LGPL-3": "LGPL-3.0",
  "MPL-1.1": "MPL-1.1",
  "MPL-2.0": "MPL-2.0",
};

export const imageLicenceDocuments: Readonly<Record<string, string>> = {
  "GPL-3.0": "/usr/share/doc/erp-ai-pro/COPYING",
  "LGPL-3.0": "/usr/share/doc/erp-ai-pro/COPYING.LESSER",
};

export type LicenceReferences = { readonly texts: string[]; readonly inImage: string[] };

export function licenceTextReferences(baseIds: Iterable<string>, table: TextTable): LicenceReferences {
  const texts: string[] = [];
  const inImage: string[] = [];
  for (const base of [...new Set(baseIds)].sort()) {
    const document = imageLicenceDocuments[base];
    if (document !== undefined) {
      inImage.push(document);
      if (base === "LGPL-3.0") inImage.push(imageLicenceDocuments["GPL-3.0"] as string);
    } else texts.push(table.add(base, canonicalText(base)));
  }
  return { texts, inImage: [...new Set(inImage)] };
}

export type DistributionRelease = { readonly id: "debian" | "ubuntu"; readonly version: string; readonly codename: string };

function debianSource(release: DistributionRelease, item: DebianPackage): string {
  return release.id === "ubuntu"
    ? `https://launchpad.net/ubuntu/+source/${encodeURIComponent(item.source)}/${encodeURIComponent(item.sourceVersion)}`
    : `https://snapshot.debian.org/package/${encodeURIComponent(item.source)}/${encodeURIComponent(item.sourceVersion)}/`;
}

function supplierOf(maintainer: string): string | undefined {
  const parsed = /^(.+?)\s*<([^>]+)>$/.exec(maintainer.trim());
  return parsed === null ? undefined : `Organization: ${parsed[1]} (${parsed[2]})`;
}

export function debianComponents(files: ImageFiles, release: DistributionRelease, table: TextTable): Component[] {
  const status = files.text("var/lib/dpkg/status");
  if (status === undefined) throw new Error("the image has no /var/lib/dpkg/status");
  const hasCommonLicences = files.list("usr/share/common-licenses").length > 0;
  return installedDebianPackages(status).map((item) => {
    const copyrightPath = `usr/share/doc/${item.name}/copyright`;
    const copyrightText = files.text(copyrightPath);
    const copyright = copyrightText === undefined ? undefined : debianCopyright(copyrightText);
    const missingTexts = hasCommonLicences ? [] : (copyright?.commonLicences ?? []).map((name) => commonLicenceTexts[name]).filter((id): id is string => id !== undefined);
    const references = licenceTextReferences(missingTexts, table);
    const named = copyright?.machineReadable === true ? `the copyright file names ${copyright.licences.join("; ")}` : undefined;
    return {
      purl: debPurl(release.id, item.name, item.version, item.architecture, `${release.id}-${release.version}`),
      kind: "deb",
      name: item.name,
      version: item.version,
      licence: debianLicenceExpression(copyright),
      copyright: [],
      texts: references.texts,
      notices: [],
      inImage: [...(copyrightText === undefined ? [] : [`/${files.resolve(copyrightPath)}`]), ...(hasCommonLicences ? (copyright?.commonLicences ?? []).map((name) => `/usr/share/common-licenses/${name}`) : []), ...references.inImage],
      source: debianSource(release, item),
      supplier: supplierOf(item.maintainer),
      detail: [
        `source package ${item.source} ${item.sourceVersion}`,
        copyrightText === undefined ? "the image carries no copyright file for it" : (named ?? "its copyright file is not machine-readable; it holds the copyright notices and the licence terms"),
      ].join("; "),
    };
  });
}

export type AlpinePackage = { readonly name: string; readonly version: string; readonly architecture: string; readonly licence: string; readonly origin: string; readonly commit: string; readonly maintainer: string };

export function installedAlpinePackages(database: string): AlpinePackage[] {
  return database
    .replace(/\r\n?/g, "\n")
    .split(/\n\s*\n/)
    .filter((block) => block.trim() !== "")
    .map((block) => {
      const fields = new Map<string, string>();
      for (const line of block.split("\n")) {
        const separator = line.indexOf(":");
        if (separator === 1 && !fields.has(line[0] as string)) fields.set(line[0] as string, line.slice(2));
      }
      const name = fields.get("P");
      const version = fields.get("V");
      if (name === undefined || version === undefined) throw new Error("an apk database entry lacks P: or V:");
      return {
        name,
        version,
        architecture: fields.get("A") ?? "",
        licence: fields.get("L") ?? "",
        origin: fields.get("o") ?? name,
        commit: fields.get("c") ?? "",
        maintainer: fields.get("m") ?? "",
      };
    });
}

export function alpineComponents(files: ImageFiles, release: string, table: TextTable): Component[] {
  const database = files.text("lib/apk/db/installed");
  if (database === undefined) throw new Error("the image has no /lib/apk/db/installed");
  return installedAlpinePackages(database).map((item) => {
    const expression = spdxExpression(item.licence);
    const references = licenceTextReferences(expression === undefined ? [] : licenceIds(expression).map(textBase), table);
    if (!/^[0-9a-f]{40}$/.test(item.commit)) throw new Error(`the apk database names no aports commit for ${item.name}`);
    return {
      purl: apkPurl(item.name, item.version, item.architecture, `alpine-${release}`),
      kind: "apk",
      name: item.name,
      version: item.version,
      licence: expression ?? (item.licence === "" ? noAssertion : item.licence),
      copyright: [],
      texts: references.texts,
      notices: [],
      inImage: references.inImage,
      source: `https://gitlab.alpinelinux.org/alpine/aports/-/tree/${item.commit}/main/${encodeURIComponent(item.origin)}`,
      supplier: supplierOf(item.maintainer)?.replace(/^Organization:/, "Person:"),
      detail: `origin ${item.origin}; Alpine packages carry no copyright file, so the copyright notices are those of the package's sources at its aports commit`,
    };
  });
}

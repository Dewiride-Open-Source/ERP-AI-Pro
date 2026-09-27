import { withoutComments } from "./code-text.ts";
import { byLocation, type WebRule, type WebSourceFile, type WebViolation } from "./web-source.ts";

type Report = (path: string, rule: WebRule, message: string) => void;

const segmentFiles = new Set([
  "layout.tsx",
  "page.tsx",
  "loading.tsx",
  "error.tsx",
  "not-found.tsx",
  "template.tsx",
  "default.tsx",
  "route.ts",
]);
const rootFiles = new Set(["layout.tsx", "page.tsx", "error.tsx", "not-found.tsx", "global-error.tsx"]);
const segmentMetadataFile =
  /^(?:icon\d*\.(?:ico|jpe?g|png|svg)|icon\.tsx?|apple-icon\d*\.(?:jpe?g|png)|apple-icon\.tsx?|(?:opengraph|twitter)-image\.(?:jpe?g|png|gif|alt\.txt|tsx?)|sitemap\.(?:xml|ts))$/;
const rootMetadataFile = /^(?:favicon\.ico|robots\.(?:txt|ts)|manifest\.(?:json|webmanifest|ts))$/;
const experimentalFile = /^(?:forbidden|unauthorized|global-not-found)\.[cm]?[jt]sx?$/;
const routeGroups = new Set(["(auth)", "(app)"]);

const group = /^\(([^()]*)\)$/;
const dynamicSegment = /^\[(?:\.\.\.)?([^[\]]*)\]$|^\[\[\.\.\.([^[\]]*)\]\]$/;
const slot = /^@(.*)$/;
const staticSegment = /^[^_()[\]@][^()[\]]*$/;

const kebabName = /^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$/;
const kebabFileName = /^[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:\.[a-z0-9]+)+$/;
const camelCaseName = /^[a-z][A-Za-z0-9]*$/;

const featureSubfolders = new Set(["components", "server", "forms", "hooks", "ai"]);
const schemaFile = /^([a-z][a-z0-9]*(?:-[a-z0-9]+)*)\.schema(\.test)?\.ts$/;
const hookFile = /^(use-[a-z0-9]+(?:-[a-z0-9]+)*)(?:\.tsx?|(\.test)\.ts)$/;
const useServerDirective = /^(["'])use server\1[ \t]*(?:;|\r?\n|$)/;
const serverOnlyImport = /^[ \t]*import[ \t]+(["'])server-only\1[ \t]*;?[ \t]*\r?$/m;

export function structureViolations(files: readonly WebSourceFile[]): WebViolation[] {
  const paths = new Set(files.map((file) => file.path));
  const found = new Map<string, WebViolation>();
  const report: Report = (path, rule, message) => found.set(`${path}\n${rule}\n${message}`, { path, rule, message });

  for (const file of files) {
    const segments = file.path.split("/");
    if (segments[0] === "app") checkApp(segments, report);
    else if (segments[0] === "features") checkFeatures(file, segments, paths, report);
    else if (segments[0] === "shared" && segments.length === 2) {
      report(file.path, "S6", "the root of shared/ holds only concern folders (shared/<concern>/)");
    }
  }

  return [...found.values()].sort(byLocation);
}

function folder(segments: readonly string[], length: number): string {
  return `${segments.slice(0, length).join("/")}/`;
}

function checkApp(segments: readonly string[], report: Report): void {
  const path = segments.join("/");
  const name = segments[segments.length - 1] ?? "";
  const directories = segments.slice(1, -1);
  const atRoot = directories.length === 0;

  directories.forEach((segment, index) => checkRouteSegment(segment, folder(segments, index + 2), report));

  if (experimentalFile.test(name)) {
    report(path, "S1", `${name} is an experimental Next.js file convention, refused until an ADR adopts it`);
    return;
  }
  if (!atRoot && (name === "global-error.tsx" || rootMetadataFile.test(name))) {
    report(path, "S1", `${name} belongs only at the root of app/`);
    return;
  }
  const special = segmentFiles.has(name) || name === "global-error.tsx" || segmentMetadataFile.test(name) || rootMetadataFile.test(name);
  if (!special) {
    report(path, "S1", `${name} is not a Next.js special file; app/ is routing-only, so the code belongs in features/ or shared/`);
    return;
  }

  if (atRoot) {
    if (!rootFiles.has(name) && !segmentMetadataFile.test(name) && !rootMetadataFile.test(name)) {
      report(
        path,
        "S3",
        "the root of app/ holds only layout.tsx, the redirect page.tsx, error.tsx, global-error.tsx, not-found.tsx and metadata files",
      );
    }
    return;
  }
  const top = directories[0] ?? "";
  if (routeGroups.has(top)) return;
  if (group.test(top)) {
    report(folder(segments, 2), "S3", "the only route groups at the root of app/ are (auth) and (app)");
  } else if (name !== "route.ts") {
    report(path, "S3", "outside (auth) and (app) a folder holds only route.ts files; pages, layouts and their UI live inside the groups");
  }
}

function checkRouteSegment(segment: string, path: string, report: Report): void {
  if (segment.startsWith("_")) {
    report(path, "S2", "private folders are not used in app/; the code belongs in features/ or shared/");
    return;
  }
  const groupName = group.exec(segment);
  const dynamicName = dynamicSegment.exec(segment);
  const slotName = slot.exec(segment);
  if (groupName) {
    if (!kebabName.test(groupName[1] ?? "")) report(path, "S6", "a route group name is kebab-case");
  } else if (dynamicName) {
    if (!camelCaseName.test(dynamicName[1] ?? dynamicName[2] ?? "")) report(path, "S6", "a dynamic segment name is a camelCase identifier");
  } else if (slotName) {
    if (!kebabName.test(slotName[1] ?? "")) report(path, "S6", "a slot name is kebab-case");
  } else if (staticSegment.test(segment)) {
    if (!kebabName.test(segment)) report(path, "S6", "a route segment is kebab-case");
  } else {
    report(path, "S2", "a folder in app/ is a route segment: name, (group), [param], [...param], [[...param]] or @slot");
  }
}

function checkFeatures(file: WebSourceFile, segments: readonly string[], paths: ReadonlySet<string>, report: Report): void {
  const path = file.path;
  const name = segments[segments.length - 1] ?? "";
  checkFeatureNames(segments, report);

  if (segments.length === 2) {
    if (name !== "registry.ts") report(path, "S4", "features/ holds registry.ts and domain folders only");
    return;
  }
  if (segments.length === 3) {
    report(path, "S4", "a domain folder holds module folders and an optional _shared/ only");
    return;
  }
  if (segments[2] === "_shared") return;

  const moduleFolder = folder(segments, 3);
  if (!paths.has(`${moduleFolder}index.ts`)) report(moduleFolder, "S4", "a module needs index.ts, its public surface");
  if (segments.length === 4) {
    if (name !== "index.ts" && name !== "nav.ts") report(path, "S4", "a module root holds index.ts, nav.ts and feature folders only");
    return;
  }
  if (segments.length === 5) {
    report(path, "S4", "a feature folder holds only components/, server/, forms/, hooks/ and ai/");
    return;
  }

  const subfolder = segments[4] ?? "";
  if (!featureSubfolders.has(subfolder)) {
    report(folder(segments, 5), "S4", `${subfolder}/ is not a feature subfolder; use components/, server/, forms/, hooks/ or ai/`);
    return;
  }
  const inside = segments.slice(5);
  if (subfolder === "server") checkServerFile(file, inside, report);
  else if (subfolder === "forms") checkFormsFile(path, inside, paths, report);
  else if (subfolder === "hooks") checkHooksFile(path, inside, paths, report);
  else if (subfolder === "ai" && inside.length === 1) {
    report(path, "S5", "ai/ holds only <capability>/ folders");
  }
}

function checkFeatureNames(segments: readonly string[], report: Report): void {
  segments.slice(1, -1).forEach((segment, index) => {
    const isDomainShared = index === 1 && segment === "_shared";
    if (!isDomainShared && !kebabName.test(segment)) {
      report(folder(segments, index + 2), "S6", "a folder under features/ is kebab-case (only a domain may hold _shared/)");
    }
  });
  const name = segments[segments.length - 1] ?? "";
  if (!kebabFileName.test(name)) report(segments.join("/"), "S6", "a file under features/ is kebab-case");
}

function checkServerFile(file: WebSourceFile, inside: readonly string[], report: Report): void {
  const [name] = inside;
  if (inside.length !== 1 || (name !== "actions.ts" && name !== "queries.ts")) {
    report(file.path, "S5", "server/ holds only actions.ts and queries.ts");
    return;
  }
  const code = withoutComments(file.content.replace(/^﻿/, ""));
  if (name === "actions.ts" && !useServerDirective.test(code.trimStart())) {
    report(file.path, "S5", 'server/actions.ts starts with the "use server" directive');
  }
  if (name === "queries.ts" && !serverOnlyImport.test(code)) {
    report(file.path, "S5", 'server/queries.ts imports "server-only"');
  }
}

function checkFormsFile(path: string, inside: readonly string[], paths: ReadonlySet<string>, report: Report): void {
  const match = inside.length === 1 ? schemaFile.exec(inside[0] ?? "") : null;
  if (!match) {
    report(path, "S5", "forms/ holds only <name>.schema.ts files and their <name>.schema.test.ts tests");
  } else if (match[2] && !paths.has(path.replace(/\.test\.ts$/, ".ts"))) {
    report(path, "S5", `${match[1]}.schema.test.ts has no ${match[1]}.schema.ts beside it`);
  }
}

function checkHooksFile(path: string, inside: readonly string[], paths: ReadonlySet<string>, report: Report): void {
  const match = inside.length === 1 ? hookFile.exec(inside[0] ?? "") : null;
  if (!match) {
    report(path, "S5", "hooks/ holds only use-<name>.ts or use-<name>.tsx files and their use-<name>.test.ts tests");
    return;
  }
  const hook = path.replace(/\.test\.ts$/, "");
  if (match[2] && !paths.has(`${hook}.ts`) && !paths.has(`${hook}.tsx`)) {
    report(path, "S5", `${match[1]}.test.ts has no ${match[1]}.ts or ${match[1]}.tsx beside it`);
  }
}

import { spawnSync } from "node:child_process";
import { existsSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";

export function globalPackagesFolder(backendRoot: string): string {
  const result = spawnSync("dotnet", ["nuget", "locals", "global-packages", "--list"], { cwd: backendRoot, encoding: "utf8" });
  const folder = /global-packages:\s*(.+)/.exec(result.stdout ?? "")?.[1]?.trim();
  if (result.status !== 0 || !folder) throw new Error(`dotnet nuget locals global-packages --list failed\n${result.stderr ?? ""}`);
  return folder;
}

export function packageDirectory(globalFolder: string, name: string, version: string): string {
  return join(globalFolder, name.toLowerCase(), version.toLowerCase());
}

export function packageEntry(directory: string, relativePath: string): string | undefined {
  let current = directory;
  for (const segment of relativePath.split(/[\\/]/).filter((part) => part.length > 0)) {
    if (!existsSync(current) || !statSync(current).isDirectory()) return undefined;
    const entry = readdirSync(current).find((candidate) => candidate.toLowerCase() === segment.toLowerCase());
    if (entry === undefined) return undefined;
    current = join(current, entry);
  }
  return statSync(current).isFile() ? current : undefined;
}

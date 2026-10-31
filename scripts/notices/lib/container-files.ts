import { spawnSync } from "node:child_process";
import { posix } from "node:path";

import { CommandFailure, collect, readCommandArchive, readTar } from "../../checks/lib/image-contents.ts";

export type ImageFile = { readonly path: string; readonly type: string; readonly linkName: string; readonly content: Uint8Array | undefined };

export type ImageDescription = { readonly env: Readonly<Record<string, string>>; readonly os: string; readonly architecture: string };

const regularTypes = new Set(["0", "\0", "7"]);

export class ImageFiles {
  readonly #files: ReadonlyMap<string, ImageFile>;

  constructor(files: ReadonlyMap<string, ImageFile>) {
    this.#files = files;
  }

  resolve(path: string): string {
    let current = posix.normalize(path).replace(/^\/+/, "");
    for (let hops = 0; hops < 40; hops += 1) {
      const segments = current.split("/");
      const linkAt = segments.findIndex((_, index) => this.#files.get(segments.slice(0, index + 1).join("/"))?.type === "2");
      if (linkAt === -1) return current;
      const link = this.#files.get(segments.slice(0, linkAt + 1).join("/")) as ImageFile;
      const target = link.linkName.startsWith("/") ? link.linkName.slice(1) : posix.join(posix.dirname(link.path), link.linkName);
      current = posix.normalize([target, ...segments.slice(linkAt + 1)].join("/"));
    }
    throw new Error(`${path} names a chain of more than 40 symbolic links`);
  }

  get(path: string): ImageFile | undefined {
    return this.#files.get(this.resolve(path));
  }

  text(path: string): string | undefined {
    const file = this.get(path);
    return file?.content === undefined ? undefined : new TextDecoder().decode(file.content);
  }

  has(path: string): boolean {
    return this.get(path) !== undefined;
  }

  list(folder: string): ImageFile[] {
    const resolved = this.resolve(folder);
    const prefix = resolved === "" ? "" : `${resolved}/`;
    return [...this.#files.values()].filter((file) => file.path.startsWith(prefix) && !file.path.slice(prefix.length).includes("/"));
  }

  all(): ImageFile[] {
    return [...this.#files.values()];
  }
}

function docker(args: readonly string[]): string {
  const result = spawnSync("docker", args, { encoding: "utf8", maxBuffer: 16 * 1024 * 1024 });
  if (result.error !== undefined) throw new Error(`docker ${args[0]} could not start: ${result.error.message}`);
  if (result.status !== 0) throw new Error(`docker ${args.join(" ")} exited with ${result.status}: ${result.stderr.trim()}`);
  return result.stdout;
}

export function describeImage(reference: string): ImageDescription {
  const inspected = JSON.parse(docker(["image", "inspect", "--format", "{{json .}}", reference])) as { Os?: string; Architecture?: string; Config?: { Env?: string[] | null } };
  const env = Object.fromEntries((inspected.Config?.Env ?? []).map((entry) => [entry.slice(0, entry.indexOf("=")), entry.slice(entry.indexOf("=") + 1)]));
  return { env, os: inspected.Os ?? "", architecture: inspected.Architecture ?? "" };
}

export function pullImage(reference: string): void {
  const present = spawnSync("docker", ["image", "inspect", "--format", "{{.Id}}", reference], { encoding: "utf8" });
  if (present.status !== 0) docker(["pull", "--quiet", reference]);
}

export async function readImageFiles(reference: string, paths: readonly string[], wanted: (path: string, size: number) => boolean = () => true): Promise<ImageFiles> {
  pullImage(reference);
  const container = docker(["create", reference, "never-started"]).trim();
  const files = new Map<string, ImageFile>();
  try {
    for (const path of paths) {
      const parent = posix.dirname(path.replace(/\/+$/, "")).replace(/^\/+/, "");
      try {
        await readCommandArchive("docker", ["cp", `${container}:${path}`, "-"], async (stdout) => {
          for await (const { header, content } of readTar(stdout)) {
            const name = posix.normalize(posix.join(parent, header.name)).replace(/\/+$/, "").replace(/^\.\/?/, "");
            const keep = regularTypes.has(header.type) && wanted(name, header.size);
            files.set(name, { path: name, type: regularTypes.has(header.type) ? "0" : header.type, linkName: header.linkName, content: keep ? await collect(content) : undefined });
          }
        });
      } catch (error) {
        if (!(error instanceof CommandFailure && /could not find the file/i.test(error.message))) throw error;
      }
    }
  } finally {
    spawnSync("docker", ["rm", container], { stdio: "ignore" });
  }
  return new ImageFiles(files);
}

export function finalBaseImage(dockerfile: string): string {
  const stages = new Map<string, string>();
  let last: string | undefined;
  for (const line of dockerfile.split(/\r?\n/)) {
    const from = /^\s*FROM\s+(?:--platform=\S+\s+)?(\S+)(?:\s+AS\s+(\S+))?\s*$/i.exec(line);
    if (from === null) continue;
    const image = stages.get((from[1] as string).toLowerCase()) ?? (from[1] as string);
    if (from[2] !== undefined) stages.set(from[2].toLowerCase(), image);
    last = image;
  }
  if (last === undefined) throw new Error("the Dockerfile has no FROM line");
  if (!/@sha256:[0-9a-f]{64}$/.test(last)) throw new Error(`the final stage of the Dockerfile starts from ${last}, which is not pinned by digest`);
  return last;
}

export function osRelease(files: ImageFiles): Record<string, string> {
  const text = files.text("usr/lib/os-release") ?? files.text("etc/os-release");
  if (text === undefined) throw new Error("the image has no os-release file");
  return Object.fromEntries(
    text
      .split(/\r?\n/)
      .map((line) => /^([A-Z_]+)=(.*)$/.exec(line))
      .filter((match): match is RegExpExecArray => match !== null)
      .map((match) => [match[1] as string, (match[2] as string).replace(/^"(.*)"$/, "$1")]),
  );
}

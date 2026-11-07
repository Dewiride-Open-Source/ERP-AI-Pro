import { existsSync, mkdirSync, readFileSync, renameSync, rmdirSync, rmSync, writeFileSync } from "node:fs";
import { dirname, relative, resolve, sep } from "node:path";

export type Change = { readonly path: string; readonly kind: "create" | "edit" };

const transientWriteErrors = new Set(["EPERM", "EBUSY", "EACCES"]);
const writeAttempts = 5;
const retryDelayMilliseconds = 200;

// Every change is planned in memory first, so a refused plan writes nothing, and a write that fails part of the way restores the
// files it edited and removes the files and folders it created.
export class ChangeSet {
  private readonly root: string;

  private readonly planned = new Map<string, { readonly kind: "create" | "edit"; readonly contents: string; readonly original?: string }>();

  private readonly refusals: string[] = [];

  constructor(root: string) {
    this.root = resolve(root);
  }

  get problems(): readonly string[] {
    return this.refusals;
  }

  get changes(): readonly Change[] {
    return [...this.planned.entries()].map(([path, { kind }]) => ({ path: this.display(path), kind }));
  }

  create(path: string, contents: string): void {
    const target = this.inside(path);
    if (target === undefined) return;
    if (this.planned.has(target) || existsSync(target)) {
      this.refusals.push(`${this.display(target)} already exists`);
      return;
    }

    this.planned.set(target, { kind: "create", contents });
  }

  edit(path: string, transform: (text: string) => string): void {
    const target = this.inside(path);
    if (target === undefined) return;
    const earlier = this.planned.get(target);
    if (earlier === undefined && !existsSync(target)) {
      this.refusals.push(`${this.display(target)} does not exist`);
      return;
    }

    const original = earlier === undefined ? readFileSync(target, "utf8") : earlier.original;
    try {
      const contents = transform(earlier?.contents ?? (original as string));
      this.planned.set(target, { kind: earlier?.kind ?? "edit", contents, original });
    } catch (error) {
      this.refusals.push(`${this.display(target)}: ${(error as Error).message}`);
    }
  }

  async apply(): Promise<void> {
    if (this.refusals.length > 0) throw new Error(`the plan was refused:\n${this.refusals.join("\n")}`);

    const edited: string[] = [];
    const created: string[] = [];
    const folders: string[] = [];
    try {
      for (const [target, change] of this.planned) {
        if (change.kind === "create") {
          folders.push(...missingFolders(dirname(target)));
          mkdirSync(dirname(target), { recursive: true });
          created.push(target);
        } else {
          edited.push(target);
        }

        await writeAtomically(target, change.contents);
      }
    } catch (error) {
      for (const target of edited) {
        const original = this.planned.get(target)?.original;
        if (original !== undefined) await writeAtomically(target, original);
      }

      for (const target of created) rmSync(target, { force: true });
      for (const folder of folders.reverse()) if (existsSync(folder)) rmdirSync(folder);
      throw error;
    }
  }

  private inside(path: string): string | undefined {
    const target = resolve(this.root, path);
    if (!target.startsWith(this.root + sep)) {
      this.refusals.push(`${path} lies outside ${this.root}`);
      return undefined;
    }

    return target;
  }

  private display(target: string): string {
    return relative(this.root, target).split(sep).join("/");
  }
}

function missingFolders(folder: string): string[] {
  const missing: string[] = [];
  for (let current = folder; !existsSync(current); current = dirname(current)) missing.unshift(current);
  return missing;
}

async function writeAtomically(target: string, contents: string): Promise<void> {
  const temporary = `${target}.scaffold-${process.pid}`;
  writeFileSync(temporary, contents, "utf8");
  for (let attempt = 1; ; attempt += 1) {
    try {
      renameSync(temporary, target);
      return;
    } catch (error) {
      const code = (error as NodeJS.ErrnoException).code ?? "";
      if (attempt >= writeAttempts || !transientWriteErrors.has(code)) {
        rmSync(temporary, { force: true });
        throw error;
      }

      await new Promise((resolveDelay) => setTimeout(resolveDelay, retryDelayMilliseconds * attempt));
    }
  }
}

import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";

export const turnFolderVariable = "E2E_TURN_FOLDER";

// The workers of a run take turns through files in a folder made for the run, which only the user running the tests can open,
// so no other user of the machine can create, read or replace a turn.
export default async function makeTurnFolder(): Promise<() => Promise<void>> {
  const folder = await mkdtemp(join(tmpdir(), "erp-e2e-turns-"));
  process.env[turnFolderVariable] = folder;
  return () => rm(folder, { recursive: true, force: true });
}

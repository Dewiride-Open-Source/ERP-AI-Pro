import type { ListRead } from "./reference-options.ts";

export interface ListSettling {
  readonly switchedOn: () => Promise<boolean>;
  readonly rethrow: (error: unknown) => void;
}

// A list that cannot be read, or whose module is switched off, is answered as such instead of thrown, so the page around a
// picker still renders and the other lists stay usable; what rethrow passes on (a redirect to sign in) still propagates.
export async function settleList<T>(
  load: () => Promise<readonly T[]>,
  settling: ListSettling,
): Promise<ListRead<T>> {
  if (!(await settling.switchedOn())) return { status: "disabled" };
  try {
    return { status: "read", items: await load() };
  } catch (error) {
    settling.rethrow(error);
    return { status: "failed" };
  }
}

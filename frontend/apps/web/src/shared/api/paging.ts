const largestInt32 = 2_147_483_647;

const pageNumber = /^[0-9]+$/;

// The API's PageRequest refuses a page whose offset, (page - 1) × pageSize, would pass the largest 32-bit integer.
export function lastRequestablePage(pageSize: number): number {
  return Math.min(largestInt32, Math.floor(largestInt32 / pageSize) + 1);
}

export function readPageParameter(
  value: string | readonly string[] | undefined,
  pageSize: number,
): number | undefined {
  if (value === undefined) return 1;
  if (typeof value !== "string" || !pageNumber.test(value)) return undefined;

  const page = Number(value);
  return page >= 1 && page <= lastRequestablePage(pageSize) ? page : undefined;
}

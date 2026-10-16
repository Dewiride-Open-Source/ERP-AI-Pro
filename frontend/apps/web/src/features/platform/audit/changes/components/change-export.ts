import { ApiError, apiErrorMessage } from "@/shared/api/problem-details";

// The API caps an export at this many lines, one for each changed value and at least one for each change, so a list of more
// changes than this cannot be downloaded whatever they hold, and the page says so before anything is sent.
export const maxExportLines = 100_000;

export const exportMediaType = "text/csv";

const exportTooLarge = "audit.export-too-large";

const rateLimitExceeded = "rate-limit.exceeded";

const indianCount = new Intl.NumberFormat("en-IN");

// The list's filter is already in the API's grammar, each value encoded once, so the whole filter is encoded once more as
// the value of its query parameter, as the API client does for the list.
export function exportPath(filter: string | undefined): `/${string}` {
  const path = "/platform/audit/changes/export";
  return filter === undefined ? path : `${path}?filter=${encodeURIComponent(filter)}`;
}

export function exportRefusal(totalCount: number): string | undefined {
  return totalCount > maxExportLines
    ? `More than ${indianCount.format(maxExportLines)} changes are listed, too many for one download. Filter the list, for example to fewer days, to download it.`
    : undefined;
}

// The API words its refusal of too many lines for any caller; the page says it with the count grouped as its lists group
// theirs.
export function exportFailureMessage(error: unknown): string {
  if (!(error instanceof ApiError)) return "The download did not finish. Try again.";
  if (error.problem.code === exportTooLarge) {
    return `These changes would fill more than ${indianCount.format(maxExportLines)} lines of the file, too many for one download. Filter the list, for example to fewer days, to download it.`;
  }
  if (error.problem.code === rateLimitExceeded) {
    return "Other downloads of the history are being prepared. Try again in a minute.";
  }
  return apiErrorMessage(error);
}

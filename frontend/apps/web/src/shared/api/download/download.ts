import { apiBasePath } from "../base-path.ts";
import { readJson } from "../browser-antiforgery.ts";
import { ApiError, problemFromBody } from "../problem-details.ts";

const problemMediaType = "application/problem+json";

// No event tells a page when the browser has finished reading the file a click saves, so its address stays valid for a
// minute instead of being released at once.
const savedFileAddressLifetimeMilliseconds = 60_000;

const extendedFileName = /(?:^|;)\s*filename\*\s*=\s*([^;]*)/i;

const plainFileName = /(?:^|;)\s*filename\s*=\s*("(?:[^"\\]|\\.)*"|[^;]*)/i;

const utf8Value = /^utf-8'[^']*'(.*)$/i;

// The browser saves the answer itself, so a Server Function never holds the whole file in the web server's memory, and a
// refusal stays on the page as an ApiError instead of replacing the ERP with the API's problem body.
export async function downloadFromApi(path: `/${string}`, mediaType: string): Promise<void> {
  const response = await fetch(`${apiBasePath}${path}`, {
    credentials: "same-origin",
    cache: "no-store",
    // The API never redirects, so a redirect means a misrouted request whose page must never be saved as the file.
    redirect: "error",
    headers: { Accept: `${mediaType}, ${problemMediaType}` },
  }).catch(() => undefined);
  if (response === undefined) {
    throw new ApiError({ status: 0, title: "The download could not reach the server." });
  }
  if (!response.ok) throw new ApiError(problemFromBody(response.status, await readJson(response)));

  const file = await response.blob().catch(() => undefined);
  if (file === undefined) {
    throw new ApiError({ status: 0, title: "The download stopped before the whole file arrived." });
  }
  saveFile(file, fileNameFrom(response.headers.get("content-disposition")));
}

// RFC 6266, section 4.3: a recipient that understands filename* (RFC 8187, UTF-8 only) prefers it to filename.
export function fileNameFrom(contentDisposition: string | null): string | undefined {
  if (contentDisposition === null) return undefined;
  return extendedValue(contentDisposition) ?? plainValue(contentDisposition);
}

function extendedValue(contentDisposition: string): string | undefined {
  const value = extendedFileName.exec(contentDisposition)?.[1]?.trim() ?? "";
  const encoded = utf8Value.exec(value)?.[1];
  if (encoded === undefined) return undefined;
  try {
    return nonEmpty(decodeURIComponent(encoded));
  } catch {
    return undefined;
  }
}

function plainValue(contentDisposition: string): string | undefined {
  const value = plainFileName.exec(contentDisposition)?.[1]?.trim() ?? "";
  const quoted = value.length >= 2 && value.startsWith('"') && value.endsWith('"');
  return nonEmpty(quoted ? value.slice(1, -1).replace(/\\(.)/g, "$1") : value);
}

function nonEmpty(text: string): string | undefined {
  return text === "" ? undefined : text;
}

function saveFile(file: Blob, fileName: string | undefined): void {
  const address = URL.createObjectURL(file);
  const link = document.createElement("a");
  link.href = address;
  link.download = fileName ?? "";
  link.hidden = true;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(address), savedFileAddressLifetimeMilliseconds);
}

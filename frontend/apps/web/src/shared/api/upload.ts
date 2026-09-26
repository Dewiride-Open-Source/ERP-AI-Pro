import { apiBasePath } from "./base-path";
import { ApiError, problemFromBody } from "./problem-details";

export type UploadProgress = { readonly loaded: number; readonly total: number };

// The browser sends the file itself: a Server Function would hold the whole file in the web server's memory before
// forwarding it and could report no progress. The request goes to this origin's /api path like every other API call.
export function uploadFile<T>(
  path: `/${string}`,
  file: File,
  onProgress: (progress: UploadProgress) => void,
): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const request = new XMLHttpRequest();
    request.open("POST", `${apiBasePath}${path}`);
    request.setRequestHeader("Accept", "application/json");
    request.upload.addEventListener("progress", (event) => {
      if (event.lengthComputable) onProgress({ loaded: event.loaded, total: event.total });
    });
    request.addEventListener("load", () => {
      const body = parseJson(request.responseText);
      if (request.status >= 200 && request.status < 300) resolve(body as T);
      else reject(new ApiError(problemFromBody(request.status, body)));
    });
    request.addEventListener("error", () =>
      reject(new ApiError({ status: 0, title: "The upload could not reach the server." })),
    );

    const form = new FormData();
    form.append("file", file, file.name);
    request.send(form);
  });
}

function parseJson(text: string): unknown {
  if (text.length === 0) return undefined;
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return undefined;
  }
}

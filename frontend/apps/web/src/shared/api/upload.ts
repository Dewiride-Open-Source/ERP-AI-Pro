import {
  antiforgeryHeaderName,
  antiforgeryRenewalPath,
  isAntiforgeryRefusal,
  sendWithAntiforgery,
  type AntiforgeryRenewal,
} from "./antiforgery";
import { apiBasePath } from "./base-path";
import { ApiError, problemFromBody } from "./problem-details";

export type UploadProgress = { readonly loaded: number; readonly total: number };

type UploadAnswer = { readonly status: number; readonly body: unknown };

const uploadMethod = "POST";

// The browser sends the file itself: a Server Function would hold the whole file in the web server's memory before
// forwarding it and could report no progress. The request goes to this origin's /api path like every other API call.
export async function uploadFile<T>(
  path: `/${string}`,
  file: File,
  onProgress: (progress: UploadProgress) => void,
): Promise<T> {
  const { status, body } = await sendWithAntiforgery<UploadAnswer>({
    method: uploadMethod,
    cookies: document.cookie,
    send: ({ token }) => sendFile(`${apiBasePath}${path}`, file, token, onProgress),
    isRefusal: (answer) => isAntiforgeryRefusal(answer.status, answer.body),
    renew: renewAntiforgeryTokens,
  });
  if (status >= 200 && status < 300) return body as T;
  throw new ApiError(problemFromBody(status, body));
}

function sendFile(
  url: string,
  file: File,
  token: string | undefined,
  onProgress: (progress: UploadProgress) => void,
): Promise<UploadAnswer> {
  return new Promise<UploadAnswer>((resolve, reject) => {
    const request = new XMLHttpRequest();
    request.open(uploadMethod, url);
    request.setRequestHeader("Accept", "application/json");
    if (token !== undefined) request.setRequestHeader(antiforgeryHeaderName, token);
    request.upload.addEventListener("progress", (event) => {
      if (event.lengthComputable) onProgress({ loaded: event.loaded, total: event.total });
    });
    request.addEventListener("load", () =>
      resolve({ status: request.status, body: parseJson(request.responseText) }),
    );
    request.addEventListener("error", () =>
      reject(new ApiError({ status: 0, title: "The upload could not reach the server." })),
    );

    const form = new FormData();
    form.append("file", file, file.name);
    request.send(form);
  });
}

// The browser stores the cookies a response sets before fetch resolves, so document.cookie read afterwards holds the renewed
// request token.
async function renewAntiforgeryTokens(): Promise<AntiforgeryRenewal<UploadAnswer>> {
  const response = await fetch(antiforgeryRenewalPath, {
    credentials: "same-origin",
    cache: "no-store",
    headers: { Accept: "application/json" },
  }).catch(() => undefined);
  if (response?.status === 204) return { outcome: "renewed", cookies: document.cookie };
  if (response?.status === 401) {
    const text = await response.text().catch(() => "");
    return { outcome: "unauthenticated", answer: { status: response.status, body: parseJson(text) } };
  }
  return { outcome: "failed" };
}

function parseJson(text: string): unknown {
  if (text.length === 0) return undefined;
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return undefined;
  }
}

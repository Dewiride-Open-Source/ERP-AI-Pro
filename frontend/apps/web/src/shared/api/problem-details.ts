import type { HttpValidationProblemDetails, ProblemDetails } from "@dewiride/erp-api-client";

type ProblemMember = "type" | "title" | "detail" | "instance" | "code" | "traceId";

export type Problem = { readonly [K in ProblemMember]?: NonNullable<ProblemDetails[K]> } & {
  readonly status: number;
  readonly fields?: Readonly<Record<string, readonly string[]>>;
};

export class ApiError extends Error {
  readonly status: number;
  readonly problem: Problem;

  constructor(problem: Problem) {
    super(problem.title ?? `API request failed with status ${problem.status}`);
    this.name = "ApiError";
    this.status = problem.status;
    this.problem = problem;
  }
}

type HttpFailure = { readonly responseStatusCode: number; readonly message?: unknown };

export function toApiError(error: unknown): unknown {
  if (!isHttpFailure(error)) return error;

  const body = error as Partial<Record<keyof HttpValidationProblemDetails, unknown>>;
  const title = text(body.title) ?? text(error.message);
  const fields = validationFields(body.errors);

  return new ApiError({
    status: error.responseStatusCode,
    ...optional("type", text(body.type)),
    ...optional("title", title),
    ...optional("detail", text(body.detail)),
    ...optional("instance", text(body.instance)),
    ...optional("code", text(body.code)),
    ...optional("traceId", text(body.traceId)),
    ...(fields ? { fields } : {}),
  });
}

function isHttpFailure(error: unknown): error is HttpFailure {
  return (
    typeof error === "object" &&
    error !== null &&
    "responseStatusCode" in error &&
    typeof error.responseStatusCode === "number"
  );
}

function validationFields(errors: unknown): Record<string, readonly string[]> | undefined {
  if (typeof errors !== "object" || errors === null || !("additionalData" in errors)) return undefined;
  const { additionalData } = errors;
  if (typeof additionalData !== "object" || additionalData === null) return undefined;

  const fields: Record<string, readonly string[]> = {};
  for (const [name, messages] of Object.entries(additionalData)) {
    if (Array.isArray(messages) && messages.every((message) => typeof message === "string")) {
      fields[name] = messages;
    }
  }
  return Object.keys(fields).length > 0 ? fields : undefined;
}

function text(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

function optional<K extends ProblemMember>(key: K, value: string | undefined): Partial<Record<K, string>> {
  return value === undefined ? {} : ({ [key]: value } as Record<K, string>);
}

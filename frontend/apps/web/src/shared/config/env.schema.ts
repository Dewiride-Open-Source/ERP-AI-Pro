import { z } from "zod";

export const developmentApiInternalUrl = "http://localhost:5080";
export const defaultAppName = "ERP-AI-Pro";
export const defaultOtelServiceName = "erp-ai-pro-web";

export class EnvironmentValidationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "EnvironmentValidationError";
  }
}

const blankToUndefined = (value: unknown) =>
  typeof value === "string" && value.trim() === "" ? undefined : value;

const withoutCredentials = (value: string) => {
  const url = new URL(value);
  return url.username === "" && url.password === "";
};

const isOrigin = (value: string) => {
  const url = new URL(value);
  return url.pathname === "/" && url.search === "" && url.hash === "";
};

const httpUrl = z.preprocess(
  (value) => (typeof value === "string" ? value.trim().replace(/\/+$/, "") : value),
  z
    .url({ protocol: /^https?$/, abort: true })
    .refine(withoutCredentials, { error: "must not carry a username or password" }),
);

const httpOrigin = httpUrl
  .refine(isOrigin, { error: "must be an origin such as http://api:8080, with no path, query or fragment" })
  .transform((value) => new URL(value).origin);

const serverSchema = z.object({
  NODE_ENV: z.preprocess(
    blankToUndefined,
    z.enum(["development", "production", "test"]).default("development"),
  ),
  API_INTERNAL_URL: z.preprocess(blankToUndefined, httpOrigin.optional()),
  OTEL_EXPORTER_OTLP_ENDPOINT: z.preprocess(blankToUndefined, httpUrl.optional()),
  OTEL_EXPORTER_OTLP_PROTOCOL: z.preprocess(
    blankToUndefined,
    z.enum(["http/protobuf", "http/json"]).default("http/protobuf"),
  ),
  OTEL_SERVICE_NAME: z.preprocess(
    blankToUndefined,
    z.string().trim().min(1).max(120).default(defaultOtelServiceName),
  ),
});

const publicSchema = z.object({
  NEXT_PUBLIC_APP_NAME: z.preprocess(
    blankToUndefined,
    z.string().trim().min(1).max(60).default(defaultAppName),
  ),
});

export type EnvironmentSource = Readonly<Record<string, string | undefined>>;

export interface ServerEnv {
  readonly nodeEnv: "development" | "production" | "test";
  readonly apiInternalUrl: string;
  readonly otelExporterOtlpEndpoint: string | undefined;
  readonly otelExporterOtlpProtocol: "http/protobuf" | "http/json";
  readonly otelServiceName: string;
}

export interface PublicEnv {
  readonly appName: string;
}

export function readServerEnv(source: EnvironmentSource): ServerEnv {
  const parsed = serverSchema.safeParse(source);
  if (!parsed.success) {
    throw new EnvironmentValidationError(`Invalid server environment:\n${z.prettifyError(parsed.error)}`);
  }

  const {
    NODE_ENV,
    API_INTERNAL_URL,
    OTEL_EXPORTER_OTLP_ENDPOINT,
    OTEL_EXPORTER_OTLP_PROTOCOL,
    OTEL_SERVICE_NAME,
  } = parsed.data;
  if (API_INTERNAL_URL === undefined && NODE_ENV === "production") {
    throw new EnvironmentValidationError(
      "Invalid server environment:\n✖ API_INTERNAL_URL is required when NODE_ENV is production (the origin of the API, such as http://api:8080)",
    );
  }

  return {
    nodeEnv: NODE_ENV,
    apiInternalUrl: API_INTERNAL_URL ?? developmentApiInternalUrl,
    otelExporterOtlpEndpoint: OTEL_EXPORTER_OTLP_ENDPOINT,
    otelExporterOtlpProtocol: OTEL_EXPORTER_OTLP_PROTOCOL,
    otelServiceName: OTEL_SERVICE_NAME,
  };
}

export function readPublicEnv(source: EnvironmentSource): PublicEnv {
  const parsed = publicSchema.safeParse(source);
  if (!parsed.success) {
    throw new EnvironmentValidationError(`Invalid public environment:\n${z.prettifyError(parsed.error)}`);
  }

  return { appName: parsed.data.NEXT_PUBLIC_APP_NAME };
}

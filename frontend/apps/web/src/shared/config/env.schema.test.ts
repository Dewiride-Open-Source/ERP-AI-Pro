import assert from "node:assert/strict";
import { test } from "node:test";

import {
  defaultAppName,
  defaultOtelServiceName,
  developmentApiInternalUrl,
  EnvironmentValidationError,
  readPublicEnv,
  readServerEnv,
} from "./env.schema.ts";

const production = (overrides: Record<string, string | undefined> = {}) => ({
  NODE_ENV: "production",
  API_INTERNAL_URL: "http://api:8080",
  ...overrides,
});

const validationError = (pattern: RegExp) => (error: unknown) =>
  error instanceof EnvironmentValidationError && pattern.test(error.message);

test("readServerEnv_ApiInternalUrlMissingInProduction_Throws", () => {
  assert.throws(
    () => readServerEnv({ NODE_ENV: "production" }),
    validationError(/API_INTERNAL_URL is required when NODE_ENV is production/),
  );
});

test("readServerEnv_ApiInternalUrlBlankInProduction_Throws", () => {
  assert.throws(
    () => readServerEnv(production({ API_INTERNAL_URL: "   " })),
    validationError(/API_INTERNAL_URL is required/),
  );
});

test("readServerEnv_ApiInternalUrlMissingInDevelopment_DefaultsToLocalhost", () => {
  const env = readServerEnv({ NODE_ENV: "development" });

  assert.equal(env.apiInternalUrl, developmentApiInternalUrl);
  assert.equal(env.nodeEnv, "development");
});

test("readServerEnv_NodeEnvMissingOrBlank_TreatedAsDevelopment", () => {
  assert.equal(readServerEnv({}).nodeEnv, "development");
  assert.equal(readServerEnv({ NODE_ENV: "" }).nodeEnv, "development");
});

test("readServerEnv_NodeEnvTest_AcceptedWithoutApiUrl", () => {
  const env = readServerEnv({ NODE_ENV: "test" });

  assert.equal(env.nodeEnv, "test");
  assert.equal(env.apiInternalUrl, developmentApiInternalUrl);
});

test("readServerEnv_NodeEnvUnknown_Throws", () => {
  assert.throws(
    () => readServerEnv({ NODE_ENV: "staging", API_INTERNAL_URL: "http://api:8080" }),
    validationError(/NODE_ENV/),
  );
});

test("readServerEnv_ApiInternalUrlWithTrailingSlashes_StripsThem", () => {
  const env = readServerEnv(production({ API_INTERNAL_URL: "http://api:8080///" }));

  assert.equal(env.apiInternalUrl, "http://api:8080");
});

test("readServerEnv_ApiInternalUrlSingleLabelHost_Accepted", () => {
  assert.equal(
    readServerEnv(production({ API_INTERNAL_URL: "http://api:8080" })).apiInternalUrl,
    "http://api:8080",
  );
  assert.equal(
    readServerEnv(production({ API_INTERNAL_URL: "https://localhost:5081" })).apiInternalUrl,
    "https://localhost:5081",
  );
});

test("readServerEnv_ApiInternalUrlWithDefaultPort_NormalisedToTheOrigin", () => {
  assert.equal(readServerEnv(production({ API_INTERNAL_URL: "HTTP://Api:80" })).apiInternalUrl, "http://api");
  assert.equal(
    readServerEnv(production({ API_INTERNAL_URL: "http://[::1]:5080/" })).apiInternalUrl,
    "http://[::1]:5080",
  );
});

test("readServerEnv_ApiInternalUrlInvalidInDevelopment_Throws", () => {
  assert.throws(
    () => readServerEnv({ NODE_ENV: "development", API_INTERNAL_URL: "not a url" }),
    validationError(/API_INTERNAL_URL/),
  );
});

test("readServerEnv_ApiInternalUrlNotAUrl_Throws", () => {
  assert.throws(
    () => readServerEnv(production({ API_INTERNAL_URL: "api:8080" })),
    validationError(/API_INTERNAL_URL/),
  );
  assert.throws(
    () => readServerEnv(production({ API_INTERNAL_URL: "not a url" })),
    validationError(/API_INTERNAL_URL/),
  );
});

test("readServerEnv_ApiInternalUrlNonHttpScheme_Throws", () => {
  assert.throws(
    () => readServerEnv(production({ API_INTERNAL_URL: "ftp://api:8080" })),
    validationError(/API_INTERNAL_URL/),
  );
  assert.throws(
    () => readServerEnv(production({ API_INTERNAL_URL: "file:///etc/passwd" })),
    validationError(/API_INTERNAL_URL/),
  );
});

test("readServerEnv_ApiInternalUrlWithPathQueryOrFragment_Throws", () => {
  for (const value of ["http://api:8080/erp", "http://api:8080/?x=1", "http://api:8080#frag"]) {
    assert.throws(
      () => readServerEnv(production({ API_INTERNAL_URL: value })),
      validationError(/must be an origin[\s\S]*at API_INTERNAL_URL/),
    );
  }
});

test("readServerEnv_ApiInternalUrlWithCredentials_ThrowsWithoutEchoingThem", () => {
  assert.throws(
    () => readServerEnv(production({ API_INTERNAL_URL: "http://svc:Sup3rS3cret@api:8080" })),
    (error: unknown) =>
      error instanceof EnvironmentValidationError &&
      /must not carry a username or password/.test(error.message) &&
      !error.message.includes("Sup3rS3cret"),
  );
});

test("readServerEnv_OtelEndpointBlank_TreatedAsUnset", () => {
  const env = readServerEnv(production({ OTEL_EXPORTER_OTLP_ENDPOINT: "" }));

  assert.equal(env.otelExporterOtlpEndpoint, undefined);
});

test("readServerEnv_OtelEndpointWithTrailingSlash_Stripped", () => {
  const env = readServerEnv(production({ OTEL_EXPORTER_OTLP_ENDPOINT: "http://aspire-dashboard:18889/" }));

  assert.equal(env.otelExporterOtlpEndpoint, "http://aspire-dashboard:18889");
});

test("readServerEnv_OtelEndpointWithPathPrefix_Accepted", () => {
  const env = readServerEnv(
    production({ OTEL_EXPORTER_OTLP_ENDPOINT: "https://collector.example.test/otlp/" }),
  );

  assert.equal(env.otelExporterOtlpEndpoint, "https://collector.example.test/otlp");
});

test("readServerEnv_OtelEndpointNonHttpScheme_Throws", () => {
  assert.throws(
    () => readServerEnv(production({ OTEL_EXPORTER_OTLP_ENDPOINT: "grpc://collector" })),
    validationError(/OTEL_EXPORTER_OTLP_ENDPOINT/),
  );
});

test("readServerEnv_OtelEndpointWithCredentials_Throws", () => {
  assert.throws(
    () => readServerEnv(production({ OTEL_EXPORTER_OTLP_ENDPOINT: "http://user:pw@collector:4318" })),
    validationError(/must not carry a username or password[\s\S]*at OTEL_EXPORTER_OTLP_ENDPOINT/),
  );
});

test("readServerEnv_OtelProtocolMissing_DefaultsToProtobuf", () => {
  assert.equal(readServerEnv(production()).otelExporterOtlpProtocol, "http/protobuf");
  assert.equal(
    readServerEnv(production({ OTEL_EXPORTER_OTLP_PROTOCOL: "http/json" })).otelExporterOtlpProtocol,
    "http/json",
  );
});

test("readServerEnv_OtelProtocolUnsupported_Throws", () => {
  assert.throws(
    () => readServerEnv(production({ OTEL_EXPORTER_OTLP_PROTOCOL: "grpc" })),
    validationError(/OTEL_EXPORTER_OTLP_PROTOCOL/),
  );
});

test("readServerEnv_OtelServiceNameMissing_DefaultsToErpAiProWeb", () => {
  assert.equal(readServerEnv(production()).otelServiceName, defaultOtelServiceName);
});

test("readServerEnv_OtelServiceNameSet_Trimmed", () => {
  assert.equal(readServerEnv(production({ OTEL_SERVICE_NAME: " erp-web " })).otelServiceName, "erp-web");
});

test("readServerEnv_OtelServiceNameAtTheLengthBound_AcceptedAndBeyondRejected", () => {
  assert.equal(
    readServerEnv(production({ OTEL_SERVICE_NAME: "s".repeat(120) })).otelServiceName,
    "s".repeat(120),
  );
  assert.throws(
    () => readServerEnv(production({ OTEL_SERVICE_NAME: "s".repeat(121) })),
    validationError(/OTEL_SERVICE_NAME/),
  );
});

test("readServerEnv_UnknownVariables_Ignored", () => {
  const env = readServerEnv(production({ PATH: "/usr/bin", SOME_OTHER: "value" }));

  assert.equal(env.apiInternalUrl, "http://api:8080");
});

test("readPublicEnv_AppNameMissingOrBlank_DefaultsToErpAiPro", () => {
  assert.equal(readPublicEnv({}).appName, defaultAppName);
  assert.equal(readPublicEnv({ NEXT_PUBLIC_APP_NAME: "" }).appName, defaultAppName);
});

test("readPublicEnv_AppNameSet_TrimmedAndKept", () => {
  assert.equal(readPublicEnv({ NEXT_PUBLIC_APP_NAME: " Dewiride ERP " }).appName, "Dewiride ERP");
});

test("readPublicEnv_AppNameAtTheLengthBound_AcceptedAndBeyondRejected", () => {
  assert.equal(readPublicEnv({ NEXT_PUBLIC_APP_NAME: "x".repeat(60) }).appName, "x".repeat(60));
  assert.throws(
    () => readPublicEnv({ NEXT_PUBLIC_APP_NAME: "x".repeat(61) }),
    validationError(/NEXT_PUBLIC_APP_NAME/),
  );
});

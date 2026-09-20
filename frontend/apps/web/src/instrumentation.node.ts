import { OTLPHttpJsonTraceExporter, OTLPHttpProtoTraceExporter, registerOTel } from "@vercel/otel";

import { EnvironmentValidationError, readServerEnv, type ServerEnv } from "./shared/config/env.schema";

export function registerNodeRuntime() {
  const env = validatedServerEnv();
  if (!env.otelExporterOtlpEndpoint) return;
  const url = `${env.otelExporterOtlpEndpoint}/v1/traces`;
  registerOTel({
    serviceName: env.otelServiceName,
    traceExporter:
      env.otelExporterOtlpProtocol === "http/json"
        ? new OTLPHttpJsonTraceExporter({ url })
        : new OTLPHttpProtoTraceExporter({ url }),
  });
}

function validatedServerEnv(): ServerEnv {
  try {
    return readServerEnv(process.env);
  } catch (error) {
    if (!(error instanceof EnvironmentValidationError)) throw error;
    console.error(`${error.message}\nThe web server cannot start until the environment is corrected.`);
    process.exit(1);
  }
}

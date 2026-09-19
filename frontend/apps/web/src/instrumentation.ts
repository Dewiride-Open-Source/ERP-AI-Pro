import { registerOTel } from "@vercel/otel";

export function register() {
  if (!process.env.OTEL_EXPORTER_OTLP_ENDPOINT) return;
  registerOTel({ serviceName: process.env.OTEL_SERVICE_NAME ?? "erp-ai-pro-web" });
}

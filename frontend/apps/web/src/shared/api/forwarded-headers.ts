export const forwardedHeaderNames = ["cookie", "traceparent", "tracestate", "x-correlation-id"] as const;

export function forwardedHeaders(incoming: { get(name: string): string | null }): Record<string, string> {
  const headers: Record<string, string> = {};
  for (const name of forwardedHeaderNames) {
    const value = incoming.get(name);
    if (value) headers[name] = value;
  }
  return headers;
}

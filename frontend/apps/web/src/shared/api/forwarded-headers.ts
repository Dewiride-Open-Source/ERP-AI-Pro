export const forwardedHeaderNames = ["cookie", "traceparent", "tracestate", "x-correlation-id"] as const;

const clientAddressHeaderName = "x-forwarded-for";

export function forwardedHeaders(incoming: { get(name: string): string | null }): Record<string, string> {
  const headers: Record<string, string> = {};
  for (const name of forwardedHeaderNames) {
    const value = incoming.get(name);
    if (value) headers[name] = value;
  }

  // The edge proxy appends the address it accepted the connection from, so only the last entry is trustworthy; the API
  // honours it only because the web container calls from a trusted network, and partitions its rate limits by it.
  const clientAddress = incoming.get(clientAddressHeaderName)?.split(",").at(-1)?.trim();
  if (clientAddress) headers[clientAddressHeaderName] = clientAddress;

  return headers;
}

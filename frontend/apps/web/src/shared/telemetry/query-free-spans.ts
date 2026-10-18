import type { Attributes, AttributeValue, Span } from "@opentelemetry/api";

type StartedSpan = Span & { readonly name: string; readonly attributes: Attributes };

export type QueryFreeSpan = { readonly name: string; readonly changedAttributes: Attributes };

// The attributes of the HTTP semantic conventions, old and current, that hold an address or a query string.
const addressAttributes = ["http.url", "http.target", "url.full"] as const;

const queryAttribute = "url.query";

export function withoutQueryString(text: string): string {
  const start = text.indexOf("?");
  return start === -1 ? text : text.slice(0, start);
}

export function queryFreeSpan(name: string, attributes: Attributes): QueryFreeSpan {
  const changedAttributes: Record<string, AttributeValue> = {};
  for (const key of addressAttributes) {
    const value = attributes[key];
    if (typeof value === "string" && withoutQueryString(value) !== value) {
      changedAttributes[key] = withoutQueryString(value);
    }
  }
  const query = attributes[queryAttribute];
  if (typeof query === "string" && query !== "") changedAttributes[queryAttribute] = "";
  return { name: withoutQueryString(name), changedAttributes };
}

// The web server's spans carry no query string: a page's address can hold what a person searched for, and the directory
// search sends the text it looks for to the API in its query, which Next.js and the fetch instrumentation copy into the name
// and the address of a span. Both record the address when the span starts, so the span is mended there, before anything
// records or exports it.
export const queryFreeSpans = {
  onStart(span: StartedSpan): void {
    const mended = queryFreeSpan(span.name, span.attributes);
    if (mended.name !== span.name) span.updateName(mended.name);
    if (Object.keys(mended.changedAttributes).length > 0) span.setAttributes(mended.changedAttributes);
  },
  onEnd(): void {
    return;
  },
  forceFlush(): Promise<void> {
    return Promise.resolve();
  },
  shutdown(): Promise<void> {
    return Promise.resolve();
  },
};

import assert from "node:assert/strict";
import { test } from "node:test";

import type { Attributes, Span } from "@opentelemetry/api";

import { queryFreeSpan, queryFreeSpans, withoutQueryString } from "./query-free-spans.ts";

const searchUrl = "http://api:8080/api/identity/users/directory?search=meera.nair%40";

function startedSpan(name: string, attributes: Attributes) {
  const span = {
    name,
    attributes: { ...attributes },
    updateName(next: string) {
      span.name = next;
      return span;
    },
    setAttributes(changed: Attributes) {
      Object.assign(span.attributes, changed);
      return span;
    },
  };
  return span;
}

test("withoutQueryString_AddressWithAQuery_KeepsWhatComesBeforeIt", () => {
  assert.equal(withoutQueryString(searchUrl), "http://api:8080/api/identity/users/directory");
  assert.equal(
    withoutQueryString("/identity/users?filter=displayName%3Acontains%3Ameera"),
    "/identity/users",
  );
  assert.equal(withoutQueryString("/identity/users?"), "/identity/users");
});

test("withoutQueryString_AddressWithoutAQuery_IsUnchanged", () => {
  assert.equal(
    withoutQueryString("http://api:8080/api/identity/users/me"),
    "http://api:8080/api/identity/users/me",
  );
  assert.equal(withoutQueryString(""), "");
});

test("queryFreeSpan_FetchSpanOfNextJs_LeavesTheSearchOutOfItsNameAndAddress", () => {
  const mended = queryFreeSpan(`fetch GET ${searchUrl}`, {
    "http.url": searchUrl,
    "http.method": "GET",
    "net.peer.name": "api",
    "next.span_type": "AppRender.fetch",
  });

  assert.deepEqual(mended, {
    name: "fetch GET http://api:8080/api/identity/users/directory",
    changedAttributes: { "http.url": "http://api:8080/api/identity/users/directory" },
  });
});

test("queryFreeSpan_RequestSpanOfAPageWithAQuery_LeavesTheQueryOutOfItsTarget", () => {
  assert.deepEqual(
    queryFreeSpan("GET", { "http.method": "GET", "http.target": "/identity/users?search=meera" }),
    {
      name: "GET",
      changedAttributes: { "http.target": "/identity/users" },
    },
  );
});

test("queryFreeSpan_SpanOfTheCurrentConventions_EmptiesItsQueryAndLeavesItOutOfItsAddress", () => {
  assert.deepEqual(queryFreeSpan("GET", { "url.full": searchUrl, "url.query": "search=meera.nair%40" }), {
    name: "GET",
    changedAttributes: { "url.full": "http://api:8080/api/identity/users/directory", "url.query": "" },
  });
});

test("queryFreeSpan_SpanWithoutAQuery_ChangesNothing", () => {
  assert.deepEqual(
    queryFreeSpan("render route (app) /profile", {
      "http.url": "http://api:8080/api/identity/users/me",
      "http.target": "/profile",
      "url.query": "",
      "next.route": "/profile",
      "http.status_code": 200,
    }),
    { name: "render route (app) /profile", changedAttributes: {} },
  );
});

test("queryFreeSpans_SpanStartedWithASearchInItsAddress_IsMendedBeforeAnythingRecordsIt", () => {
  const span = startedSpan(`fetch GET ${searchUrl}`, { "http.url": searchUrl, "http.method": "GET" });

  queryFreeSpans.onStart(span as unknown as Span & typeof span);

  assert.equal(span.name, "fetch GET http://api:8080/api/identity/users/directory");
  assert.deepEqual(span.attributes, {
    "http.url": "http://api:8080/api/identity/users/directory",
    "http.method": "GET",
  });
});

test("queryFreeSpans_SpanWithoutAQuery_IsLeftAsItStarted", () => {
  const span = startedSpan("GET", { "http.target": "/" });
  let changes = 0;
  const watched = {
    ...span,
    updateName: () => {
      changes += 1;
      return watched;
    },
    setAttributes: () => {
      changes += 1;
      return watched;
    },
  };

  queryFreeSpans.onStart(watched as unknown as Span & typeof span);

  assert.equal(changes, 0);
});

import assert from "node:assert/strict";
import { test } from "node:test";

import { forwardedHeaders } from "./forwarded-headers.ts";

test("forwardedHeaders_IncomingCookieAndTraceContext_ForwardsThem", () => {
  const incoming = new Headers({
    cookie: ".AspNetCore.Cookies=abc",
    traceparent: "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
    tracestate: "vendor=value",
    "x-correlation-id": "order-4711",
  });

  assert.deepEqual(forwardedHeaders(incoming), {
    cookie: ".AspNetCore.Cookies=abc",
    traceparent: "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
    tracestate: "vendor=value",
    "x-correlation-id": "order-4711",
  });
});

test("forwardedHeaders_OtherIncomingHeaders_AreNotForwarded", () => {
  const incoming = new Headers({
    authorization: "Bearer should-not-leave",
    host: "erp.example",
    "x-forwarded-host": "evil.example",
    "x-forwarded-proto": "https",
    accept: "text/html",
  });

  assert.deepEqual(forwardedHeaders(incoming), {});
});

test("forwardedHeaders_EmptyValue_IsNotForwarded", () => {
  assert.deepEqual(forwardedHeaders(new Headers({ cookie: "" })), {});
});

test("forwardedHeaders_ForwardedChain_ForwardsOnlyTheAddressTheEdgeProxyAppended", () => {
  const incoming = new Headers({ "x-forwarded-for": "198.51.100.1, 203.0.113.7" });

  assert.deepEqual(forwardedHeaders(incoming), { "x-forwarded-for": "203.0.113.7" });
});

test("forwardedHeaders_SingleForwardedAddress_ForwardsItTrimmed", () => {
  const incoming = new Headers({ "x-forwarded-for": " 2001:db8::7 " });

  assert.deepEqual(forwardedHeaders(incoming), { "x-forwarded-for": "2001:db8::7" });
});

test("forwardedHeaders_BlankLastForwardedEntry_IsNotForwarded", () => {
  assert.deepEqual(forwardedHeaders(new Headers({ "x-forwarded-for": "203.0.113.7, " })), {});
});

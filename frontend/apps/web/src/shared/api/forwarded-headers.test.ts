import assert from "node:assert/strict";
import { test } from "node:test";

import { forwardedHeaders, withoutForwardedHeaders } from "./forwarded-headers.ts";

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

test("withoutForwardedHeaders_ForwardingHeaders_RemovesEveryOne", () => {
  const incoming = new Headers({
    Forwarded: "for=198.51.100.1;proto=https;host=evil.example",
    "X-Forwarded-For": "198.51.100.1, 203.0.113.7",
    "x-forwarded-host": "evil.example",
    "x-forwarded-proto": "https",
    "x-forwarded-port": "443",
    "x-forwarded-prefix": "/evil",
  });

  assert.deepEqual([...withoutForwardedHeaders(incoming).keys()], []);
});

test("withoutForwardedHeaders_OtherHeaders_KeepsThemUnchanged", () => {
  const incoming = new Headers({
    accept: "application/json",
    cookie: ".AspNetCore.Cookies=abc",
    traceparent: "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
    tracestate: "vendor=value",
    "x-correlation-id": "order-4711",
    "x-forwarded-for": "203.0.113.7",
    "x-real-ip": "203.0.113.7",
  });

  assert.deepEqual(Object.fromEntries(withoutForwardedHeaders(incoming)), {
    accept: "application/json",
    cookie: ".AspNetCore.Cookies=abc",
    traceparent: "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
    tracestate: "vendor=value",
    "x-correlation-id": "order-4711",
    "x-real-ip": "203.0.113.7",
  });
});

test("withoutForwardedHeaders_IncomingHeaders_AreLeftIntact", () => {
  const incoming = new Headers({ cookie: ".AspNetCore.Cookies=abc", "x-forwarded-host": "evil.example" });

  withoutForwardedHeaders(incoming);

  assert.equal(incoming.get("x-forwarded-host"), "evil.example");
  assert.equal(incoming.get("cookie"), ".AspNetCore.Cookies=abc");
});

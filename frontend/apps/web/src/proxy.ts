import { NextResponse, type NextRequest } from "next/server";

import { apiBasePath } from "./shared/api/base-path";
import { readServerEnv, type ServerEnv } from "./shared/config/env.schema";

const isDevelopment = process.env.NODE_ENV === "development";
const apiPrefixes = [`${apiBasePath}/`, "/openapi/"];

let cachedEnv: ServerEnv | undefined;

function serverEnv(): ServerEnv {
  cachedEnv ??= readServerEnv(process.env);
  return cachedEnv;
}

export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  if (apiPrefixes.some((prefix) => pathname.startsWith(prefix))) {
    return NextResponse.rewrite(new URL(`${pathname}${search}`, serverEnv().apiInternalUrl), {
      request: { headers: withoutForwardedHeaders(request.headers) },
    });
  }

  const nonce = Buffer.from(crypto.randomUUID()).toString("base64");
  const secure =
    request.nextUrl.protocol === "https:" || request.headers.get("x-forwarded-proto") === "https";
  const csp = contentSecurityPolicy(nonce, secure);

  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("x-nonce", nonce);
  requestHeaders.set("Content-Security-Policy", csp);

  const response = NextResponse.next({ request: { headers: requestHeaders } });
  response.headers.set("Content-Security-Policy", csp);
  response.headers.set("Referrer-Policy", "strict-origin-when-cross-origin");
  response.headers.set("X-Content-Type-Options", "nosniff");
  response.headers.set(
    "Permissions-Policy",
    "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()",
  );
  return response;
}

function withoutForwardedHeaders(incoming: Headers): Headers {
  const headers = new Headers(incoming);
  for (const name of [...headers.keys()]) {
    if (name.startsWith("x-forwarded-") || name === "forwarded") headers.delete(name);
  }
  return headers;
}

function contentSecurityPolicy(nonce: string, secure: boolean): string {
  const scriptSources = [
    "'self'",
    `'nonce-${nonce}'`,
    "'strict-dynamic'",
    ...(isDevelopment ? ["'unsafe-eval'"] : []),
  ];
  const directives = [
    "default-src 'self'",
    `script-src ${scriptSources.join(" ")}`,
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' blob: data:",
    "font-src 'self' data:",
    "connect-src 'self'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
    ...(secure ? ["upgrade-insecure-requests"] : []),
  ];
  return directives.join("; ");
}

export const config = {
  matcher: [
    {
      source: "/((?!_next/static|_next/image|healthz|icon.svg).*)",
      missing: [
        { type: "header", key: "next-router-prefetch" },
        { type: "header", key: "purpose", value: "prefetch" },
      ],
    },
  ],
};

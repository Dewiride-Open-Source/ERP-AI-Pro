import { NextResponse, type NextRequest } from "next/server";

import { apiBasePath } from "./shared/api/base-path";
import { withoutForwardedHeaders } from "./shared/api/forwarded-headers";
import {
  loginAddressWithoutForeignReturn,
  sessionCookieName,
  signInRedirectFor,
} from "./shared/auth/page-access";
import { loginPath, pagePathHeaderName } from "./shared/auth/sign-in-addresses";
import { readServerEnv, type ServerEnv } from "./shared/config/env.schema";
import { contentSecurityPolicy, isSecureRequest } from "./shared/security/content-security-policy";

const isDevelopment = process.env.NODE_ENV === "development";
const apiPrefixes = [`${apiBasePath}/`, "/openapi/"];

let cachedEnv: ServerEnv | undefined;

function serverEnv(): ServerEnv {
  cachedEnv ??= readServerEnv(process.env);
  return cachedEnv;
}

export function proxy(request: NextRequest) {
  const { pathname, search, searchParams } = request.nextUrl;
  if (apiPrefixes.some((prefix) => pathname.startsWith(prefix))) {
    return NextResponse.rewrite(new URL(`${pathname}${search}`, serverEnv().apiInternalUrl), {
      request: { headers: withoutForwardedHeaders(request.headers) },
    });
  }

  const signIn = signInRedirectFor({
    method: request.method,
    pathname,
    search,
    hasSessionCookie: request.cookies.has(sessionCookieName),
  });
  if (signIn !== undefined) return NextResponse.redirect(new URL(signIn, request.url));

  if (pathname === loginPath) {
    const login = loginAddressWithoutForeignReturn(searchParams);
    if (login !== undefined) return NextResponse.redirect(new URL(login, request.url));
  }

  const nonce = Buffer.from(crypto.randomUUID()).toString("base64");
  const csp = contentSecurityPolicy({
    nonce,
    secure: isSecureRequest(request.nextUrl, request.headers),
    development: isDevelopment,
  });

  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("x-nonce", nonce);
  requestHeaders.set("Content-Security-Policy", csp);
  requestHeaders.set(pagePathHeaderName, `${pathname}${search}`);

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

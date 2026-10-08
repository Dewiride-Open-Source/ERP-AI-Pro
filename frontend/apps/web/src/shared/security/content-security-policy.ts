export type ContentSecurityPolicyOptions = {
  readonly nonce: string;
  readonly secure: boolean;
  readonly development: boolean;
};

// Chrome checks every redirect a form submission follows against form-action, and the sign-out form's answer redirects to
// the tenant's end-session endpoint of Microsoft Entra ID in the public cloud, the instance every app registration of the ERP
// uses. CSP Level 3 ignores a source's path once a request has been redirected (section 7.6, "Paths and Redirects"), so that
// redirect passes while a form on the page cannot post to any other Microsoft address.
const entraEndSessionSource = "https://login.microsoftonline.com/common/oauth2/v2.0/logout";

export function contentSecurityPolicy({ nonce, secure, development }: ContentSecurityPolicyOptions): string {
  const scriptSources = [
    "'self'",
    `'nonce-${nonce}'`,
    "'strict-dynamic'",
    ...(development ? ["'unsafe-eval'"] : []),
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
    `form-action 'self' ${entraEndSessionSource}`,
    "frame-ancestors 'none'",
    ...(secure ? ["upgrade-insecure-requests"] : []),
  ];
  return directives.join("; ");
}

export function isSecureRequest(
  url: { readonly protocol: string },
  headers: { get(name: string): string | null },
): boolean {
  return url.protocol === "https:" || headers.get("x-forwarded-proto") === "https";
}

export type ContentSecurityPolicyOptions = {
  readonly nonce: string;
  readonly secure: boolean;
  readonly development: boolean;
};

// Chrome checks every redirect a form submission follows against form-action, and the sign-out form's answer redirects to
// the end-session endpoint of Microsoft Entra ID in the public cloud, the instance every app registration of the ERP uses.
const microsoftSignInOrigin = "https://login.microsoftonline.com";

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
    `form-action 'self' ${microsoftSignInOrigin}`,
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

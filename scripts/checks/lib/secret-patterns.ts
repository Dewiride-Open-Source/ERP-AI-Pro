export interface SecretPattern {
  readonly name: string;
  readonly reason: string;
  readonly pattern: RegExp;
  readonly valueGroup?: number;
}

export interface Finding {
  readonly path: string;
  readonly line: number;
  readonly name: string;
  readonly reason: string;
  readonly excerpt: string;
}

export type TextEncoding = "utf8" | "utf16le" | "utf16be";

const credentialKeys = ["client secret", "api key", "access key", "secret key", "password", "pwd"];

function credentialKeyAlternatives(key: string): string[] {
  const words = key.split(" ");
  const camel = words.map((word) => `[${word[0]?.toUpperCase()}${word[0]}]${word.slice(1)}`).join("[_-]?");
  const upper = words.map((word) => word.toUpperCase()).join("[_-]?");
  return [camel, upper];
}

const credentialKeyPattern = credentialKeys.flatMap(credentialKeyAlternatives).join("|");

export const secretPatterns: readonly SecretPattern[] = [
  {
    name: "private-key",
    reason: "private key material",
    pattern: /-----BEGIN (?:RSA |EC |DSA |OPENSSH |ENCRYPTED |PGP )?PRIVATE KEY(?: BLOCK)?-----/,
  },
  {
    name: "entra-client-secret",
    reason: "Entra client secret shape",
    pattern: /(?<![A-Za-z0-9_~.+/-])[A-Za-z0-9_~.-]{3}[78]Q~[A-Za-z0-9_~.-]{31,34}(?![A-Za-z0-9_~.+/-])/,
  },
  {
    name: "appconfig-connection-string",
    reason: "App Configuration connection string with an access key",
    pattern: /Endpoint=https?:\/\/[^;\s]+;Id=[^;\s]+;Secret=([^\s"']+)/,
    valueGroup: 1,
  },
  {
    name: "azure-storage-key",
    reason: "Azure Storage account key",
    pattern: /AccountKey=([A-Za-z0-9+/=]{86,})/,
    valueGroup: 1,
  },
  {
    name: "github-token",
    reason: "GitHub token",
    pattern: /\b(?:gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{70,})\b/,
  },
  {
    name: "jwt",
    reason: "JSON web token",
    pattern: /\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b/,
  },
  {
    name: "authorization-header",
    reason: "Authorization header with a credential",
    pattern: /\bAuthorization:\s*(?:Basic|Bearer)\s+([A-Za-z0-9+/=_.-]{16,})/i,
    valueGroup: 1,
  },
  {
    name: "base64-der-blob",
    reason: "base64 DER blob (certificate or key)",
    pattern: /\bMII[A-Za-z0-9+/=]{97,}/,
  },
  {
    name: "credential-assignment",
    reason: "credential value assigned in text",
    pattern: new RegExp(String.raw`(?:(?<![A-Za-z0-9])|(?<=[a-z])(?=[A-Z]))(?:${credentialKeyPattern})\b["']?\s*[:=]\s*["']?([^\s"';,]{8,})`),
    valueGroup: 1,
  },
];

const placeholderWords = new Set(["CHANGE", "CHANGEME", "PLACEHOLDER", "REDACTED", "EXAMPLE", "SECRET", "PASSWORD", "YOUR", "XXX"]);
const memberCall = /^[A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*)+\(/;

export function isPlaceholder(value: string): boolean {
  const trimmed = value.trim();
  if (/^<[^>]+>$/.test(trimmed) || /^\$\{[^}]+\}$/.test(trimmed) || /^\{\{[^}]+\}\}$/.test(trimmed) || /^%[^%]+%$/.test(trimmed)) return true;
  if (/^\$[A-Za-z_][A-Za-z0-9_]*$/.test(trimmed)) return true;
  const words = trimmed.toUpperCase().split(/[^A-Z0-9]+/);
  if (words.some((word) => placeholderWords.has(word) || /^X{3,}$/.test(word))) return true;
  if (/^(.)\1+$/.test(trimmed)) return true;
  if (!/\d/.test(trimmed)) return true;
  return memberCall.test(trimmed);
}

const binaryExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".svg", ".woff", ".woff2", ".ttf", ".otf", ".pdf", ".pfx", ".p12", ".zip", ".gz"];
const skippedPaths = ["pnpm-lock.yaml", "packages.lock.json", "docs/roadmap/ROADMAP.md"];

export function textEncoding(head: Uint8Array): TextEncoding | undefined {
  const sample = head.subarray(0, 8192);
  if (sample[0] === 0xff && sample[1] === 0xfe) return "utf16le";
  if (sample[0] === 0xfe && sample[1] === 0xff) return "utf16be";
  if (!sample.includes(0)) return "utf8";
  const pairs = Math.floor(sample.length / 2);
  if (pairs < 2) return undefined;
  let evenZero = 0;
  let oddZero = 0;
  for (let index = 0; index < pairs; index += 1) {
    if (sample[2 * index] === 0) evenZero += 1;
    if (sample[2 * index + 1] === 0) oddZero += 1;
  }
  if (oddZero >= pairs * 0.9 && evenZero === 0) return "utf16le";
  if (evenZero >= pairs * 0.9 && oddZero === 0) return "utf16be";
  return undefined;
}

export function isScannable(repoPath: string, head: Uint8Array): boolean {
  const lower = repoPath.toLowerCase();
  if (binaryExtensions.some((extension) => lower.endsWith(extension))) return false;
  if (skippedPaths.some((skipped) => repoPath === skipped || repoPath.endsWith(`/${skipped}`))) return false;
  return textEncoding(head) !== undefined;
}

export function decodeText(content: Uint8Array, encoding: TextEncoding): string {
  const buffer = Buffer.from(content.buffer, content.byteOffset, content.byteLength);
  switch (encoding) {
    case "utf8":
      return buffer.toString("utf8");
    case "utf16le":
      return buffer.toString("utf16le").replace(/^﻿/, "");
    case "utf16be":
      return Buffer.from(buffer).swap16().toString("utf16le").replace(/^﻿/, "");
  }
}

export function scanText(repoPath: string, content: string): Finding[] {
  const findings: Finding[] = [];
  content.split(/\r?\n/).forEach((line, index) => {
    for (const secret of secretPatterns) {
      const match = secret.pattern.exec(line);
      if (!match) continue;
      const value = secret.valueGroup === undefined ? match[0] : (match[secret.valueGroup] ?? "");
      if (secret.valueGroup !== undefined && isPlaceholder(value)) continue;
      findings.push({
        path: repoPath,
        line: index + 1,
        name: secret.name,
        reason: secret.reason,
        excerpt: `${value.slice(0, 8)}…`,
      });
    }
  });
  return findings;
}

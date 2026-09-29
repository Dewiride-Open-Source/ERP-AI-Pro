import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

// The store-less gated API refuses to start without a sign-in certificate and can never complete a Microsoft sign-in, so it
// gets a self-signed certificate made for this run, which no app registration knows and which is deleted once read.
export function throwawaySignInCertificate(): string {
  const folder = mkdtempSync(join(tmpdir(), "erp-e2e-sign-in-"));
  try {
    const key = join(folder, "key.pem");
    const certificate = join(folder, "certificate.pem");
    const pkcs12 = join(folder, "certificate.pfx");
    openssl([
      "req",
      "-x509",
      "-newkey",
      "rsa:2048",
      "-noenc",
      "-days",
      "2",
      "-subj",
      "/CN=erp-e2e",
      "-keyout",
      key,
      "-out",
      certificate,
    ]);
    openssl(["pkcs12", "-export", "-inkey", key, "-in", certificate, "-passout", "pass:", "-out", pkcs12]);
    return readFileSync(pkcs12).toString("base64");
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
}

function openssl(args: readonly string[]): void {
  try {
    execFileSync("openssl", args, { stdio: ["ignore", "ignore", "pipe"], encoding: "utf8" });
  } catch (error) {
    if (error instanceof Error && "code" in error && error.code === "ENOENT") {
      throw new Error(
        "openssl was not found: Playwright needs it for the gated API's throwaway sign-in certificate; run pnpm e2e from Git Bash, which ships openssl, or put openssl on the PATH",
        { cause: error },
      );
    }
    const reason =
      error instanceof Error && "stderr" in error && typeof error.stderr === "string"
        ? error.stderr.trim()
        : "";
    throw new Error(
      `openssl ${args[0]} could not make the gated API's throwaway sign-in certificate: ${reason}`,
      { cause: error },
    );
  }
}

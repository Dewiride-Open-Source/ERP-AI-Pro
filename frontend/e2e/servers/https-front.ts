import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import {
  Agent,
  request,
  type IncomingHttpHeaders,
  type IncomingMessage,
  type OutgoingHttpHeaders,
  type ServerResponse,
} from "node:http";
import { createServer, type ServerOptions } from "node:https";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { pipeline } from "node:stream";

type Route = { port: number; upstream: URL };

const loopbackHosts = ["127.0.0.1", "::1"];

const hopByHopHeaders: ReadonlySet<string> = new Set([
  "connection",
  "keep-alive",
  "proxy-connection",
  "te",
  "trailer",
  "transfer-encoding",
  "upgrade",
]);

const idleConnectionTimeout = 60_000;

// next start closes an idle connection when its keep-alive timeout runs out, and a request sent on a connection it is closing
// at that moment fails with ECONNRESET, which the front could only answer with 502; a new loopback connection per request
// costs nothing.
const upstreamAgent = new Agent({ keepAlive: false });

function routes(args: readonly string[]): Route[] {
  if (args.length === 0)
    throw new Error(
      "name at least one <https port>=<upstream origin> pair, such as 3200=http://127.0.0.1:3100",
    );
  return args.map((arg) => {
    const [port, upstream] = arg.split("=", 2);
    const number = Number(port);
    if (
      !Number.isInteger(number) ||
      number < 1 ||
      number > 65_535 ||
      upstream === undefined ||
      !URL.canParse(upstream)
    ) {
      throw new Error(
        `"${arg}" is not an <https port>=<upstream origin> pair, such as 3200=http://127.0.0.1:3100`,
      );
    }
    return { port: number, upstream: new URL(upstream) };
  });
}

function localhostCertificate(): ServerOptions {
  const folder = mkdtempSync(join(tmpdir(), "erp-e2e-tls-"));
  try {
    const key = join(folder, "key.pem");
    const cert = join(folder, "certificate.pem");
    execFileSync(
      "openssl",
      [
        "req",
        "-x509",
        "-newkey",
        "rsa:2048",
        "-noenc",
        "-days",
        "2",
        "-subj",
        "/CN=localhost",
        "-addext",
        "subjectAltName=DNS:localhost,IP:127.0.0.1,IP:::1",
        "-keyout",
        key,
        "-out",
        cert,
      ],
      { stdio: ["ignore", "ignore", "pipe"] },
    );
    return { key: readFileSync(key), cert: readFileSync(cert) };
  } catch (error) {
    throw new Error(
      error instanceof Error && "code" in error && error.code === "ENOENT"
        ? "openssl was not found: the https front needs it for its certificate; run Playwright from Git Bash, which ships openssl, or put openssl on the PATH"
        : `openssl could not make the https front's certificate: ${String(error)}`,
      { cause: error },
    );
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
}

function endToEndHeaders(headers: IncomingHttpHeaders): OutgoingHttpHeaders {
  const listed = new Set(
    String(headers.connection ?? "")
      .split(",")
      .map((name) => name.trim().toLowerCase()),
  );
  return Object.fromEntries(
    Object.entries(headers).filter(
      ([name, value]) => value !== undefined && !hopByHopHeaders.has(name) && !listed.has(name),
    ),
  );
}

function forwardedFor(incoming: IncomingMessage): string {
  const client = incoming.socket.remoteAddress ?? "";
  const earlier = incoming.headers["x-forwarded-for"];
  return earlier ? `${String(earlier)}, ${client}` : client;
}

function forward(upstream: URL) {
  return (incoming: IncomingMessage, outgoing: ServerResponse): void => {
    const proxied = request(
      {
        protocol: upstream.protocol,
        hostname: upstream.hostname,
        port: upstream.port,
        method: incoming.method,
        path: incoming.url,
        agent: upstreamAgent,
        headers: {
          ...endToEndHeaders(incoming.headers),
          "x-forwarded-proto": "https",
          "x-forwarded-host": incoming.headers.host ?? "",
          "x-forwarded-for": forwardedFor(incoming),
        },
      },
      (answer) => {
        outgoing.writeHead(answer.statusCode ?? 502, answer.statusMessage, endToEndHeaders(answer.headers));
        pipeline(answer, outgoing, (error) => {
          if (error) outgoing.destroy(error);
        });
      },
    );
    proxied.on("error", (error) => {
      if (outgoing.headersSent) {
        outgoing.destroy(error);
        return;
      }
      outgoing.writeHead(502, { "content-type": "text/plain; charset=utf-8" });
      outgoing.end(`The https front could not reach ${upstream.origin}: ${error.message}`);
    });
    outgoing.on("close", () => {
      if (!outgoing.writableFinished) proxied.destroy();
    });
    pipeline(incoming, proxied, (error) => {
      if (error) outgoing.destroy(error);
    });
  };
}

const certificate = localhostCertificate();
for (const { port, upstream } of routes(process.argv.slice(2))) {
  for (const host of loopbackHosts) {
    const server = createServer(certificate, forward(upstream));
    server.keepAliveTimeout = idleConnectionTimeout;
    server.on("error", (error: NodeJS.ErrnoException) => {
      if (host !== loopbackHosts[0] && (error.code === "EADDRNOTAVAIL" || error.code === "EAFNOSUPPORT"))
        return;
      throw error;
    });
    server.listen(port, host);
  }
  console.log(`https front: https://localhost:${port} -> ${upstream.origin}`);
}

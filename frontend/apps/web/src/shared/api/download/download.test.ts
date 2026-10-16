import assert from "node:assert/strict";
import { afterEach, test, type TestContext } from "node:test";

import { ApiError } from "../problem-details.ts";

import { downloadFromApi, fileNameFrom } from "./download.ts";

type SentRequest = { readonly url: string; readonly init: RequestInit | undefined };

class SavedLink {
  href = "";
  download: string | undefined = undefined;
  hidden = false;
  attached = false;
  clicksWhileAttached = 0;

  click(): void {
    if (this.attached) this.clicksWhileAttached += 1;
  }

  remove(): void {
    this.attached = false;
  }
}

type Browser = {
  readonly requests: SentRequest[];
  readonly links: SavedLink[];
  readonly files: Blob[];
  readonly released: string[];
};

const exportPath = "/platform/audit/changes/export?filter=entityType%3Aeq%3Aidentity.users.people";

const fileAddress = "blob:https://localhost:3200/0b7c6f1e";

const byteOrderMark = String.fromCodePoint(0xfeff);

const csv = `${byteOrderMark}"Time (IST)","Action"\r\n`;

const oneMinute = 60_000;

function browser(t: TestContext, answer: () => Promise<Response>): Browser {
  const requests: SentRequest[] = [];
  const links: SavedLink[] = [];
  const files: Blob[] = [];
  const released: string[] = [];
  const document = {
    body: {
      append(link: SavedLink) {
        link.attached = true;
      },
    },
    createElement(tagName: string) {
      assert.equal(tagName, "a");
      const link = new SavedLink();
      links.push(link);
      return link;
    },
  };
  Object.defineProperty(globalThis, "document", { value: document, configurable: true, writable: true });
  t.mock.method(globalThis, "fetch", async (input: RequestInfo | URL, init?: RequestInit) => {
    requests.push({ url: String(input), init });
    return answer();
  });
  t.mock.method(URL, "createObjectURL", (file: Blob) => {
    files.push(file);
    return fileAddress;
  });
  t.mock.method(URL, "revokeObjectURL", (address: string) => {
    released.push(address);
  });
  t.mock.timers.enable({ apis: ["setTimeout"] });
  return { requests, links, files, released };
}

function csvAnswer(headers: Record<string, string>): () => Promise<Response> {
  return async () => new Response(csv, { status: 200, headers: { "content-type": "text/csv", ...headers } });
}

function problemAnswer(status: number, problem: Record<string, unknown>): () => Promise<Response> {
  return async () =>
    new Response(JSON.stringify({ status, ...problem }), {
      status,
      headers: { "content-type": "application/problem+json" },
    });
}

afterEach(() => {
  Reflect.deleteProperty(globalThis, "document");
});

test("downloadFromApi_File_AsksTheApiOnThisOriginForTheMediaTypeOrAProblem", async (t) => {
  const { requests } = browser(
    t,
    csvAnswer({ "content-disposition": "attachment; filename=change-history.csv" }),
  );

  await downloadFromApi(exportPath, "text/csv");

  assert.equal(requests.length, 1);
  assert.equal(requests[0]?.url, `/api${exportPath}`);
  assert.equal(requests[0]?.init?.method, undefined);
  assert.equal(requests[0]?.init?.credentials, "same-origin");
  assert.equal(requests[0]?.init?.cache, "no-store");
  assert.equal(requests[0]?.init?.redirect, "error");
  assert.deepEqual(requests[0]?.init?.headers, { Accept: "text/csv, application/problem+json" });
});

test("downloadFromApi_File_SavesItThroughAHiddenLinkUnderTheNameTheAnswerGives", async (t) => {
  const { links, files } = browser(
    t,
    csvAnswer({
      "content-disposition":
        "attachment; filename=change-history-20261016-143000.csv; filename*=UTF-8''change-history-20261016-143000.csv",
    }),
  );

  await downloadFromApi(exportPath, "text/csv");

  assert.equal(links.length, 1);
  assert.equal(links[0]?.href, fileAddress);
  assert.equal(links[0]?.download, "change-history-20261016-143000.csv");
  assert.equal(links[0]?.hidden, true);
  assert.equal(links[0]?.clicksWhileAttached, 1);
  assert.equal(links[0]?.attached, false);
  assert.equal(files.length, 1);
  assert.deepEqual(
    new Uint8Array((await files[0]?.arrayBuffer()) ?? new ArrayBuffer(0)),
    new TextEncoder().encode(csv),
  );
});

test("downloadFromApi_SavedFile_ReleasesItsAddressAfterAMinute", async (t) => {
  const { released } = browser(t, csvAnswer({ "content-disposition": "attachment; filename=changes.csv" }));

  await downloadFromApi(exportPath, "text/csv");
  t.mock.timers.tick(oneMinute - 1);

  assert.deepEqual(released, []);

  t.mock.timers.tick(1);

  assert.deepEqual(released, [fileAddress]);
});

test("downloadFromApi_AnswerWithoutAFileName_LetsTheBrowserNameTheFile", async (t) => {
  const { links } = browser(t, csvAnswer({}));

  await downloadFromApi(exportPath, "text/csv");

  assert.equal(links[0]?.download, "");
  assert.equal(links[0]?.clicksWhileAttached, 1);
});

test("downloadFromApi_ProblemAnswer_RejectsWithTheProblemAndSavesNothing", async (t) => {
  const { links, files } = browser(
    t,
    problemAnswer(422, {
      code: "audit.export-too-large",
      title: "The export is too large.",
      detail: "Narrow the filters to at most 100,000 lines.",
      traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    }),
  );

  await assert.rejects(downloadFromApi(exportPath, "text/csv"), (error: unknown) => {
    assert.ok(error instanceof ApiError);
    assert.equal(error.status, 422);
    assert.equal(error.problem.code, "audit.export-too-large");
    assert.equal(error.problem.detail, "Narrow the filters to at most 100,000 lines.");
    assert.equal(error.problem.traceId, "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
    return true;
  });
  assert.deepEqual(links, []);
  assert.deepEqual(files, []);
});

test("downloadFromApi_EndedSession_RejectsWithTheUnauthorizedStatus", async (t) => {
  browser(t, problemAnswer(401, { code: "request.unauthenticated" }));

  await assert.rejects(downloadFromApi(exportPath, "text/csv"), (error: unknown) => {
    assert.ok(error instanceof ApiError);
    assert.equal(error.status, 401);
    return true;
  });
});

test("downloadFromApi_RefusalWithoutAProblemBody_RejectsWithTheStatusAlone", async (t) => {
  browser(t, async () => new Response("<html>Bad Gateway</html>", { status: 502 }));

  await assert.rejects(downloadFromApi(exportPath, "text/csv"), (error: unknown) => {
    assert.ok(error instanceof ApiError);
    assert.deepEqual(error.problem, { status: 502 });
    return true;
  });
});

test("downloadFromApi_RequestThatNeverArrives_RejectsAsUnreachable", async (t) => {
  const { links } = browser(t, async () => {
    throw new TypeError("Failed to fetch");
  });

  await assert.rejects(downloadFromApi(exportPath, "text/csv"), (error: unknown) => {
    assert.ok(error instanceof ApiError);
    assert.equal(error.status, 0);
    assert.equal(error.message, "The download could not reach the server.");
    return true;
  });
  assert.deepEqual(links, []);
});

test("downloadFromApi_AnswerThatBreaksOff_RejectsAndSavesNothing", async (t) => {
  const { links, files } = browser(
    t,
    async () =>
      new Response(
        new ReadableStream({
          start(controller) {
            controller.enqueue(new TextEncoder().encode(csv));
            controller.error(new TypeError("network error"));
          },
        }),
        { status: 200, headers: { "content-type": "text/csv" } },
      ),
  );

  await assert.rejects(downloadFromApi(exportPath, "text/csv"), (error: unknown) => {
    assert.ok(error instanceof ApiError);
    assert.equal(error.status, 0);
    assert.equal(error.message, "The download stopped before the whole file arrived.");
    return true;
  });
  assert.deepEqual(links, []);
  assert.deepEqual(files, []);
});

test("fileNameFrom_NoHeader_IsNothing", () => {
  assert.equal(fileNameFrom(null), undefined);
});

test("fileNameFrom_PlainName_IsTheName", () => {
  assert.equal(fileNameFrom("attachment; filename=change-history.csv"), "change-history.csv");
  assert.equal(fileNameFrom("attachment;FileName=  report.csv  "), "report.csv");
});

test("fileNameFrom_QuotedName_IsUnquotedAndUnescaped", () => {
  assert.equal(fileNameFrom('attachment; filename="Q2; \\"final\\" report.csv"'), 'Q2; "final" report.csv');
});

test("fileNameFrom_ExtendedName_IsPreferredAndDecoded", () => {
  assert.equal(
    fileNameFrom("attachment; filename=_.csv; filename*=UTF-8''%E0%A4%AC%E0%A4%BF%E0%A4%B2.csv"),
    "बिल.csv",
  );
  assert.equal(fileNameFrom("attachment; filename*=utf-8'en'%E2%82%B9%20rates.csv"), "₹ rates.csv");
});

test("fileNameFrom_ExtendedNameInAnotherCharacterSet_FallsBackToThePlainName", () => {
  assert.equal(
    fileNameFrom("attachment; filename=rates.csv; filename*=ISO-8859-1''%A3%20rates.csv"),
    "rates.csv",
  );
});

test("fileNameFrom_ExtendedNameThatCannotBeDecoded_FallsBackToThePlainName", () => {
  assert.equal(fileNameFrom("attachment; filename=rates.csv; filename*=UTF-8''%E0%A4.csv"), "rates.csv");
});

test("fileNameFrom_NoNameGiven_IsNothing", () => {
  assert.equal(fileNameFrom("attachment"), undefined);
  assert.equal(fileNameFrom("attachment; filename="), undefined);
  assert.equal(fileNameFrom('attachment; filename=""'), undefined);
  assert.equal(fileNameFrom("attachment; filename*=UTF-8''"), undefined);
});

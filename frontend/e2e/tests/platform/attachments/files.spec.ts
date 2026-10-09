import { randomUUID } from "node:crypto";

import type { Page, Request, Response } from "@playwright/test";

import { png, type FileUpload } from "../../../fixtures/files";
import { holdServerFunctionCalls } from "../../../fixtures/server-functions";
import {
  endEntraSession,
  requestToken,
  requestTokenCookie,
  requestTokenHeader,
  signIn,
} from "../../../fixtures/sign-in";
import { expect, forEachTheme, signedInApi, test, type SignedInApi } from "../../../fixtures/test";
import { isSignInPage, LoginPage, sessionEndedNotice } from "../../../pages/identity/auth/login.page";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import { AttachmentsPage, attachmentsPath } from "../../../pages/platform/attachments/files.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

const attachmentsApi = "/api/platform/attachments";

const antiforgeryRenewalApi = "/api/auth/antiforgery";

const listPageSize = 20;

function isUploadRequest(request: Request): boolean {
  return request.method() === "POST" && new URL(request.url()).pathname === attachmentsApi;
}

function isUpload(response: Response): boolean {
  return isUploadRequest(response.request());
}

function watchUploads(page: Page): { count: () => number } {
  let uploads = 0;
  page.on("request", (request) => {
    if (isUploadRequest(request)) uploads += 1;
  });
  return { count: () => uploads };
}

async function holdUploads(page: Page): Promise<() => void> {
  let release: () => void = () => undefined;
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(`**${attachmentsApi}`, async (route) => {
    if (!isUploadRequest(route.request())) {
      await route.fallback();
      return;
    }
    await released;
    await route.continue();
  });
  return release;
}

async function createdAttachmentId(response: {
  status(): number;
  json(): Promise<unknown>;
}): Promise<string> {
  expect(response.status()).toBe(201);
  const { id } = (await response.json()) as { id?: unknown };
  expect(typeof id).toBe("string");
  return id as string;
}

async function seedAttachments(api: SignedInApi, count: number, ids: string[]): Promise<void> {
  for (let index = 0; index < count; index += 1) {
    const response = await api.post(attachmentsApi, { multipart: { file: png() } });
    ids.push(await createdAttachmentId(response));
  }
}

async function deleteAttachments(api: SignedInApi, ids: readonly string[]): Promise<void> {
  for (const id of ids) {
    const response = await api.delete(`${attachmentsApi}/${id}`);
    expect(response.status()).toBe(204);
  }
}

// A test that deletes some of its files itself cleans up whatever is left, so a failure midway reports its own cause rather
// than a cleanup that found a file already gone.
async function deleteLeftoverAttachments(api: SignedInApi, ids: readonly string[]): Promise<void> {
  for (const id of ids) {
    const response = await api.delete(`${attachmentsApi}/${id}`);
    expect([204, 404]).toContain(response.status());
  }
}

test.describe("attachments page", () => {
  forEachTheme("uploads, lists, downloads and deletes a file", async ({ page, capture }) => {
    const attachments = new AttachmentsPage(page);
    const file = png();
    await attachments.goto();

    await expect(page).toHaveTitle(/Attachments · ERP-AI-Pro/);
    await expect(attachments.unavailable).toHaveCount(0);
    await expect(attachments.dropZone).toMatchAriaSnapshot(`
      - group "Drop a file here or choose one":
        - paragraph: Drop a file here or choose one
        - paragraph: /PDF, .*PNG image.*; up to \\d+ MB\\./
        - button "Choose a file"
    `);
    await capture("attachments-before-upload");

    // WebKit sometimes forwards an intercepted multipart request with an empty file part, which the API rightly refuses,
    // so only the other engines hold the upload open to check the uploading state; WebKit uploads straight through.
    const holdsUpload = page.context().browser()?.browserType().name() !== "webkit";
    const release = holdsUpload ? await holdUploads(page) : () => undefined;
    const uploaded = page.waitForResponse(isUpload);
    await attachments.chooseAndUpload(file);
    if (holdsUpload) {
      await expect(attachments.uploadStatus).toHaveAttribute("data-state", "uploading");
      await expect(attachments.uploadAnnouncement).toHaveText(`Uploading ${file.name}…`);
      await expect(attachments.uploadProgress).toHaveAccessibleName(`Uploading ${file.name}`);
      await expect(attachments.uploadProgress).toHaveAttribute("aria-valuenow", /^\d+$/);
      await expect(attachments.chooseFile).toBeDisabled();
      await capture("attachments-uploading");
    }
    release();

    expect((await uploaded).status()).toBe(201);
    await expect(attachments.uploadAnnouncement).toHaveText(`${file.name} was uploaded.`);
    await expect(attachments.uploadProgress).toHaveCount(0);
    const row = attachments.row(file.name);
    await expect(row).toBeVisible();
    await expect(row).toContainText("PNG image");
    await expect(row).toContainText("256 bytes");
    if (await attachments.list.showsCards()) {
      await expect(row).toMatchAriaSnapshot(`
        - listitem:
          - text: /^e2e-[0-9a-f]{12}\\.png$/
          - term: Type
          - definition: PNG image
          - term: Size
          - definition: 256 bytes
          - term: Uploaded
          - definition: /\\d{4}/
          - term: Virus scan
          - definition: Not scanned
          - button /^Download e2e-[0-9a-f]{12}\\.png$/
          - button /^Delete e2e-[0-9a-f]{12}\\.png$/
      `);
    } else {
      await expect(attachments.list.table.getByRole("row").first()).toMatchAriaSnapshot(`
        - row:
          - columnheader "File":
            - button "File"
          - columnheader "Type":
            - button "Type"
          - columnheader "Size":
            - button "Size"
          - columnheader "Uploaded":
            - button "Uploaded"
          - columnheader "Virus scan"
          - columnheader "Actions"
      `);
      await expect(attachments.list.columnHeader("Uploaded")).toHaveAttribute("aria-sort", "descending");
      await expect(row).toMatchAriaSnapshot(`
        - row:
          - rowheader /^e2e-[0-9a-f]{12}\\.png$/
          - cell "PNG image"
          - cell "256 bytes"
          - cell /\\d{4}/
          - cell "Not scanned"
          - cell:
            - button /^Download e2e-[0-9a-f]{12}\\.png$/
            - button /^Delete e2e-[0-9a-f]{12}\\.png$/
      `);
    }
    await capture("attachments-uploaded");

    const download = await attachments.download(row);
    expect(download.name).toBe(file.name);
    expect(Buffer.compare(download.content, file.buffer)).toBe(0);
    await expect(page).toHaveURL((url) => url.pathname === attachmentsPath);
    await expect(attachments.heading).toBeVisible();
    await expect(row.getByTestId("attachment-action-error")).toHaveCount(0);

    await row.getByTestId("attachment-delete").click();
    await expect(attachments.deleteDialog).toBeVisible();
    await expect(attachments.deleteDialog).toContainText(file.name);
    await capture("attachments-delete-dialog");
    await attachments.cancelDelete.click();
    await expect(attachments.deleteDialog).toHaveCount(0);
    await expect(row).toBeVisible();
    await expect(row.getByTestId("attachment-delete")).toBeFocused();

    await row.getByTestId("attachment-delete").click();
    await attachments.confirmDelete.click();
    await expect(attachments.listHeading).toBeFocused();
    await expect(row).toHaveCount(0);
    await expect(new AppShell(page).toast(`Deleted ${file.name}.`)).toBeVisible();
  });

  test("takes a deleted file off the list at once and treats one deleted elsewhere as gone", async ({
    page,
    api,
  }) => {
    const attachments = new AttachmentsPage(page);
    const shell = new AppShell(page);
    const prefix = `e2e-delete-${randomUUID().slice(-12)}`;
    const kept = png({ name: `${prefix}-first.png` });
    const gone = png({ name: `${prefix}-second.png` });
    const ids = new Map<string, string>();
    for (const file of [kept, gone]) {
      ids.set(file.name, await createdAttachmentId(await api.post(attachmentsApi, { multipart: { file } })));
    }

    try {
      await attachments.goto(`?name=${prefix}`);
      await attachments.list.waitUntilInteractive();
      await expect(attachments.list.status).toContainText("Showing all 2 files.");

      const release = await holdServerFunctionCalls(page, attachmentsPath);
      await attachments.row(kept.name).getByTestId("attachment-delete").click();
      await attachments.confirmDelete.click();
      await expect(attachments.row(kept.name)).toHaveCount(0);
      await expect(attachments.list.status).toContainText("Showing 1 file.");
      release();
      await expect(shell.toast(`Deleted ${kept.name}.`)).toBeVisible();
      await expect(attachments.row(kept.name)).toHaveCount(0);

      const deletedElsewhere = await api.delete(`${attachmentsApi}/${ids.get(gone.name) ?? ""}`);
      expect(deletedElsewhere.status()).toBe(204);
      await attachments.row(gone.name).getByTestId("attachment-delete").click();
      await attachments.confirmDelete.click();
      const alreadyGone = shell.toast(`${gone.name} is no longer stored.`);
      await expect(alreadyGone).toBeVisible();
      await expect(alreadyGone).toContainText("It had already been deleted.");
      await expect(attachments.row(gone.name)).toHaveCount(0);
      await expect(attachments.list.noMatches).toBeVisible();
    } finally {
      await deleteLeftoverAttachments(api, [...ids.values()]);
    }
  });

  forEachTheme("uploads a file dropped on the drop zone", async ({ page, capture }) => {
    const attachments = new AttachmentsPage(page);
    const file = png();
    await attachments.goto();

    const dataTransfer = await attachments.transferOf(file);
    await expect(async () => {
      await attachments.dragOver(dataTransfer);
      await expect(attachments.dropZone).toHaveAttribute("data-dragging", "true", { timeout: 1_000 });
    }).toPass();
    await capture("attachments-dragging");

    const uploaded = page.waitForResponse(isUpload);
    await attachments.drop(dataTransfer);
    const id = await createdAttachmentId(await uploaded);

    try {
      await expect(attachments.dropZone).not.toHaveAttribute("data-dragging");
      await expect(attachments.uploadAnnouncement).toHaveText(`${file.name} was uploaded.`);
      await expect(attachments.row(file.name)).toBeVisible();
    } finally {
      await deleteAttachments(signedInApi(page.context()), [id]);
    }
  });

  forEachTheme(
    "refuses an empty file and a type that cannot be uploaded before sending it",
    async ({ page, capture }) => {
      const attachments = new AttachmentsPage(page);
      const uploads = watchUploads(page);
      await attachments.goto();

      await attachments.upload({ name: "empty.png", mimeType: "image/png", buffer: Buffer.alloc(0) });
      await expect(attachments.uploadError).toContainText("empty.png: the file is empty.");

      await attachments.upload({
        name: "setup.exe",
        mimeType: "application/x-msdownload",
        buffer: Buffer.from("MZ"),
      });
      await expect(attachments.uploadError).toContainText(
        "setup.exe: files of this type cannot be uploaded.",
      );
      await capture("attachments-refused");

      expect(uploads.count()).toBe(0);
    },
  );

  test.describe("when the API refuses the file", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 415/ });

    forEachTheme("shows the API's reason for refusing the upload", async ({ page, capture }) => {
      const attachments = new AttachmentsPage(page);
      const file: FileUpload = {
        name: `fake-${randomUUID().slice(-12)}.png`,
        mimeType: "image/png",
        buffer: Buffer.from("not a png"),
      };
      await attachments.goto();

      const refused = page.waitForResponse(isUpload);
      await attachments.chooseAndUpload(file);

      expect((await refused).status()).toBe(415);
      await expect(attachments.uploadError).toHaveText(
        `${file.name}: The file's content does not match its declared type.`,
      );
      await expect(attachments.uploadStatus).toHaveAttribute("data-state", "failed");
      await expect(attachments.chooseFile).toBeEnabled();
      await expect(attachments.row(file.name)).toHaveCount(0);
      await capture("attachments-refused-by-api");
    });
  });

  test.describe("when the API refuses the upload's antiforgery token", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 400/ });

    test("renews the token once and sends the upload once more with the renewed token", async ({
      page,
      baseURL,
      context,
    }) => {
      const attachments = new AttachmentsPage(page);
      const file = png();
      // WebKit sometimes forwards an intercepted multipart request with an empty file part, so there the upload sent once
      // more is answered with the API's answer to the same file sent by the test.
      const continuesInterceptedUploads = page.context().browser()?.browserType().name() !== "webkit";
      const sentTokens: (string | undefined)[] = [];
      const stored: Response[] = [];
      let renewals = 0;
      page.on("response", (response) => {
        if (isUpload(response) && response.status() === 201) stored.push(response);
      });
      const renewed = sessionRenewed(page);
      await attachments.goto();
      await renewed;
      // The first upload carries a token the API never issued, as after a sign-in in another tab, and the renewal is the
      // API's own, whose pair the upload sent once more must carry. The page's own session renewal is answered first, or it
      // would carry that token too and renew the pair a second time.
      await page
        .context()
        .addCookies([
          { name: requestTokenCookie, value: "first-token", url: baseURL!, secure: true, sameSite: "Strict" },
        ]);

      await page.route(`**${antiforgeryRenewalApi}`, async (route) => {
        renewals += 1;
        await route.continue();
      });
      await page.route(`**${attachmentsApi}`, async (route) => {
        if (!isUploadRequest(route.request())) {
          await route.fallback();
          return;
        }
        sentTokens.push(route.request().headers()["x-xsrf-token"]);
        if (sentTokens.length === 1) {
          await route.fulfill({
            status: 400,
            contentType: "application/problem+json",
            json: {
              type: "/problems/antiforgery.token-missing",
              title: "The request could not be confirmed as coming from this site.",
              status: 400,
              code: "antiforgery.token-missing",
            },
          });
          return;
        }
        if (continuesInterceptedUploads) {
          await route.continue();
          return;
        }
        await route.fulfill({
          response: await page.request.post(attachmentsApi, {
            multipart: { file },
            headers: { [requestTokenHeader]: route.request().headers()[requestTokenHeader] ?? "" },
          }),
        });
      });

      try {
        await attachments.chooseAndUpload(file);

        await expect(attachments.uploadAnnouncement).toHaveText(`${file.name} was uploaded.`);
        await expect(attachments.uploadStatus).toHaveAttribute("data-state", "uploaded");
        await expect(attachments.row(file.name)).toBeVisible();
        expect(renewals).toBe(1);
        const renewedToken = await requestToken(context);
        expect(renewedToken).not.toBe("first-token");
        expect(sentTokens).toEqual(["first-token", renewedToken]);
      } finally {
        await deleteAttachments(
          signedInApi(page.context()),
          await Promise.all(stored.map(createdAttachmentId)),
        );
      }
    });
  });

  test.describe("with the request token", () => {
    test("uploads, downloads and deletes a file carrying the request token, and refuses a change without it", async ({
      page,
      context,
    }) => {
      // A signed-in person's changes are checked for the antiforgery token: the page sends the upload itself, with the token
      // in the header, and the web server sends the download link and the delete on its behalf, which the API accepts only
      // with it.
      const attachments = new AttachmentsPage(page);
      const file = png();
      await attachments.goto();

      const uploadSent = page.waitForRequest(isUploadRequest);
      const uploaded = page.waitForResponse(isUpload);
      await attachments.chooseAndUpload(file);
      expect((await uploadSent).headers()[requestTokenHeader]).toBe(await requestToken(context));
      const id = await createdAttachmentId(await uploaded);
      const row = attachments.row(file.name);
      await expect(row).toBeVisible();

      const download = await attachments.download(row);
      expect(download.name).toBe(file.name);
      expect(Buffer.compare(download.content, file.buffer)).toBe(0);
      await expect(row.getByTestId("attachment-action-error")).toHaveCount(0);

      const withoutToken = await page.request.delete(`${attachmentsApi}/${id}`);
      expect(withoutToken.status()).toBe(400);
      expect(await withoutToken.json()).toMatchObject({
        type: "/problems/antiforgery.token-missing",
        code: "antiforgery.token-missing",
      });

      await row.getByTestId("attachment-delete").click();
      await attachments.confirmDelete.click();
      await expect(row).toHaveCount(0);
      await expect(new AppShell(page).toast(`Deleted ${file.name}.`)).toBeVisible();
    });
  });

  test.describe("once Microsoft has ended this browser's session", () => {
    test("a delete goes to the sign-in page, comes back to the list there and deletes nothing", async ({
      api,
      baseURL,
      context,
      entraSession,
      page,
      request,
    }) => {
      const attachments = new AttachmentsPage(page);
      const file = png({ name: `e2e-ended-${randomUUID().slice(-12)}.png` });
      const query = `?name=${file.name}`;
      const id = await createdAttachmentId(await api.post(attachmentsApi, { multipart: { file } }));

      try {
        const renewed = sessionRenewed(page);
        await attachments.goto(query);
        await attachments.list.waitUntilInteractive();
        await renewed;
        await expect(attachments.row(file.name)).toBeVisible();
        await endEntraSession(request, entraSession);

        await attachments.row(file.name).getByTestId("attachment-delete").click();
        await attachments.confirmDelete.click();

        await expect(page).toHaveURL(
          isSignInPage(new URL(baseURL ?? "").origin, `${attachmentsPath}${query}`, "session-ended"),
        );
        await expect(new LoginPage(page).notice).toHaveText(sessionEndedNotice);

        await signIn(context.request, "accountant");
        await attachments.goto(query);
        await expect(attachments.row(file.name)).toBeVisible();
      } finally {
        // The open page renews its session once it has loaded, and a sign-in through the same browser context while that
        // renewal is under way leaves the page sending one person's request token with the next person's cookie.
        await page.goto("about:blank");
        await signIn(context.request, "accountant");
        await deleteLeftoverAttachments(api, [id]);
      }
    });
  });

  test("refuses a file over the upload limit before sending it", async ({ page, browserName, isMobile }) => {
    test.skip(browserName !== "chromium" || isMobile, "one engine is enough for a 25 MB buffer");
    const attachments = new AttachmentsPage(page);
    const uploads = watchUploads(page);
    await attachments.goto();

    await attachments.upload(png({ size: 25 * 1024 * 1024 + 1 }));

    await expect(attachments.uploadError).toContainText("the file is larger than 25 MB.");
    expect(uploads.count()).toBe(0);
  });

  test("uploads a file larger than the web server's default request buffer", async ({
    page,
    browserName,
    isMobile,
  }) => {
    test.skip(browserName !== "chromium" || isMobile, "one engine is enough for a 12 MB transfer");
    const attachments = new AttachmentsPage(page);
    const file = png({ size: 12 * 1024 * 1024 });
    await attachments.goto();

    await attachments.upload(file);

    await expect(attachments.uploadStatus).toContainText(`${file.name} was uploaded.`, { timeout: 60_000 });
    const row = attachments.row(file.name);
    await expect(row).toContainText("12 MB");

    await row.getByTestId("attachment-delete").click();
    await attachments.confirmDelete.click();
    await expect(row).toHaveCount(0);
  });

  test.describe("across pages", () => {
    test("moves between pages and brings a page outside the list back to one that exists", async ({
      page,
      api,
    }) => {
      test.slow();
      const attachments = new AttachmentsPage(page);
      const seeded: string[] = [];

      try {
        await seedAttachments(api, listPageSize + 1, seeded);
        await attachments.goto();
        const { list } = attachments;
        await list.waitUntilInteractive();
        await expect(list.pageLabel).toHaveText(/^Page 1 of \d+$/);
        await expect(list.previousPage).toHaveAttribute("aria-disabled", "true");
        await expect(await list.rows()).toHaveCount(listPageSize);

        await list.nextPage.click();
        await expect(page).toHaveURL((url) => url.searchParams.get("page") === "2");
        await expect(list.pageLabel).toHaveText(/^Page 2 of \d+$/);
        await expect((await list.rows()).first()).toBeVisible();

        await list.previousPage.click();
        await expect(page).toHaveURL((url) => url.pathname === attachmentsPath && url.search === "");
        await expect(list.pageLabel).toHaveText(/^Page 1 of \d+$/);
        await expect((await list.rows()).first()).toBeVisible();

        await page.goto(`${attachmentsPath}?page=100000`);
        await expect(page).toHaveURL((url) => {
          const landed = Number(url.searchParams.get("page"));
          return url.pathname === attachmentsPath && landed >= 2 && landed < 100_000;
        });
        const landed = new URL(page.url()).searchParams.get("page") ?? "";
        await expect(list.pageLabel).toHaveText(new RegExp(`^Page ${landed} of \\d+$`));
        await expect((await list.rows()).first()).toBeVisible();

        for (const malformed of ["abc", "0", "-2", "1.5", "999999999999"]) {
          await page.goto(`${attachmentsPath}?page=${malformed}`);
          await expect(page).toHaveURL((url) => url.pathname === attachmentsPath && url.search === "");
          await expect(list.pageLabel).toHaveText(/^Page 1 of \d+$/);
        }
      } finally {
        await deleteAttachments(api, seeded);
      }
    });
  });

  test("sorts, filters and hides a column of the stored files through the API", async ({ page, api }) => {
    const attachments = new AttachmentsPage(page);
    const { list } = attachments;
    const token = `e2e-${randomUUID().slice(-8)}`;
    const small = png({ size: 256, name: `${token}-small.png` });
    const large = png({ size: 512, name: `${token}-large.png` });
    const seeded: string[] = [];

    try {
      for (const file of [small, large]) {
        seeded.push(await createdAttachmentId(await api.post(attachmentsApi, { multipart: { file } })));
      }
      await attachments.goto();
      await list.waitUntilInteractive();

      const name = list.textFilter("File name");
      await name.fill(token);
      await name.press("Enter");
      await expect(page).toHaveURL((url) => url.searchParams.get("name") === token);
      await expect(list.status).toContainText("Showing all 2 files.");
      await expect(name).toBeFocused();

      await list.sortBy("Size", "Size, highest first");
      await expect(page).toHaveURL((url) => url.searchParams.get("sort") === "sizeBytes:desc");
      await expect.poll(() => list.titles()).toEqual([large.name, small.name]);
      await list.sortBy("Size", "Size, lowest first");
      await expect(page).toHaveURL((url) => url.searchParams.get("sort") === "sizeBytes:asc");
      await expect.poll(() => list.titles()).toEqual([small.name, large.name]);

      await list.rangeStart("Uploaded between").fill(indiaDate(-1));
      await list.rangeEnd("Uploaded between").fill(indiaDate(0));
      await list.applyFilters.click();
      await expect(page).toHaveURL(
        (url) => url.searchParams.has("uploadedFrom") && url.searchParams.has("uploadedTo"),
      );
      await expect(list.status).toContainText("Showing all 2 files.");

      await list.chooseOptions("Type", ["JPEG image"]);
      await list.applyFilters.click();
      await expect(page).toHaveURL((url) => url.searchParams.get("type") === "image/jpeg");
      await expect(list.noMatches).toContainText("No files match these filters.");

      await list.optionsFilter("Type").click();
      await page.getByRole("menuitemcheckbox", { name: "JPEG image", exact: true }).click();
      await page.getByRole("menuitemcheckbox", { name: "PNG image", exact: true }).click();
      await page.keyboard.press("Escape");
      await list.applyFilters.click();
      await expect(page).toHaveURL((url) => url.searchParams.get("type") === "image/png");
      await expect(list.status).toContainText("Showing all 2 files.");

      await list.setColumnVisible("Type", false);
      await expect(page).toHaveURL((url) => url.searchParams.get("hide") === "contentType");
      if (await list.showsCards()) {
        await expect(attachments.row(small.name).getByRole("term")).toHaveText([
          "Size",
          "Uploaded",
          "Virus scan",
        ]);
      } else {
        await expect(list.columnHeader("Type")).toHaveCount(0);
      }
    } finally {
      await deleteAttachments(api, seeded);
    }
  });
});

function indiaDate(daysFromToday: number): string {
  const day = new Date(Date.now() + daysFromToday * 86_400_000);
  return new Intl.DateTimeFormat("en-GB", {
    timeZone: "Asia/Kolkata",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  })
    .format(day)
    .replaceAll("/", "-");
}

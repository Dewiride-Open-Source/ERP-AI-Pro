import { randomUUID } from "node:crypto";
import { readFile } from "node:fs/promises";

import type { APIRequestContext, Page, Request, Response } from "@playwright/test";

import { expect, forEachTheme, test } from "../../../fixtures/test";
import {
  AttachmentsPage,
  attachmentsPath,
  type UploadFile,
} from "../../../pages/platform/attachments/files.page";

const attachmentsApi = "/api/platform/attachments";

const listPageSize = 20;

const pngSignature = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

const filler = Buffer.from(Array.from({ length: 251 }, (_, index) => index));

// Content is fixed per size and only the name is unique, so repeated runs deduplicate to one stored file each.
function png(size = 256): UploadFile {
  const name = `e2e-${randomUUID().slice(-12)}.png`;
  return {
    name,
    mimeType: "image/png",
    buffer: Buffer.concat([pngSignature, Buffer.alloc(size - pngSignature.length, filler)]),
  };
}

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

async function seedAttachments(request: APIRequestContext, count: number, ids: string[]): Promise<void> {
  for (let index = 0; index < count; index += 1) {
    const response = await request.post(attachmentsApi, { multipart: { file: png() } });
    ids.push(await createdAttachmentId(response));
  }
}

async function deleteAttachments(request: APIRequestContext, ids: readonly string[]): Promise<void> {
  for (const id of ids) {
    const response = await request.delete(`${attachmentsApi}/${id}`);
    expect(response.status()).toBe(204);
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
    await expect(attachments.table).toMatchAriaSnapshot(`
      - table:
        - rowgroup:
          - row:
            - columnheader "File"
            - columnheader "Size"
            - columnheader "Actions"
        - rowgroup:
          - row /e2e-[0-9a-f]{12}\\.png/:
            - cell /e2e-[0-9a-f]{12}\\.png PNG image/
            - cell /^[\\d,.]+ (bytes?|KB|MB|GB)$/
            - cell:
              - button /^Download e2e-[0-9a-f]{12}\\.png$/
              - button /^Delete e2e-[0-9a-f]{12}\\.png$/
    `);
    await capture("attachments-uploaded");

    const downloadStarted = page.waitForEvent("download");
    await row.getByTestId("attachment-download").click();
    const download = await downloadStarted;
    expect(download.suggestedFilename()).toBe(file.name);
    expect(Buffer.compare(await readFile((await download.path())!), file.buffer)).toBe(0);
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
      await deleteAttachments(page.request, [id]);
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
      const file: UploadFile = {
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

  test("refuses a file over the upload limit before sending it", async ({ page, browserName, isMobile }) => {
    test.skip(browserName !== "chromium" || isMobile, "one engine is enough for a 25 MB buffer");
    const attachments = new AttachmentsPage(page);
    const uploads = watchUploads(page);
    await attachments.goto();

    await attachments.upload(png(25 * 1024 * 1024 + 1));

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
    const file = png(12 * 1024 * 1024);
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
    // WebKit reports a router prefetch that a navigation cancels as an access-control failure, and this test navigates while
    // the previous page is still prefetching its links; a same-origin request cannot fail an access-control check otherwise.
    test.use({ expectedConsoleError: /\?_rsc=[\w-]+ due to access control checks\.$/ });

    test("moves between pages and brings a page outside the list back to one that exists", async ({
      page,
      request,
    }) => {
      test.slow();
      const attachments = new AttachmentsPage(page);
      const seeded: string[] = [];

      try {
        await seedAttachments(request, listPageSize + 1, seeded);
        await attachments.goto();
        await expect(attachments.pagination).toBeVisible();
        await expect(attachments.currentPage).toHaveText(/^Page 1 of \d+$/);
        await expect(attachments.pagination.getByRole("button", { name: "Previous" })).toBeDisabled();
        await expect(attachments.rows).toHaveCount(listPageSize);

        await attachments.nextPage.click();
        await expect(page).toHaveURL((url) => url.searchParams.get("page") === "2");
        await expect(attachments.currentPage).toHaveText(/^Page 2 of \d+$/);
        await expect(attachments.rows.first()).toBeVisible();

        await attachments.previousPage.click();
        await expect(page).toHaveURL((url) => url.searchParams.get("page") === "1");
        await expect(attachments.currentPage).toHaveText(/^Page 1 of \d+$/);
        await expect(attachments.rows.first()).toBeVisible();

        await page.goto(`${attachmentsPath}?page=100000`);
        await expect(page).toHaveURL((url) => {
          const landed = Number(url.searchParams.get("page"));
          return url.pathname === attachmentsPath && landed >= 2 && landed < 100_000;
        });
        const landed = new URL(page.url()).searchParams.get("page") ?? "";
        await expect(attachments.currentPage).toHaveText(new RegExp(`^Page ${landed} of \\d+$`));
        await expect(attachments.rows.first()).toBeVisible();

        for (const malformed of ["abc", "0", "-2", "1.5", "999999999999"]) {
          await page.goto(`${attachmentsPath}?page=${malformed}`);
          await expect(page).toHaveURL((url) => url.pathname === attachmentsPath && url.search === "");
          await expect(attachments.currentPage).toHaveText(/^Page 1 of \d+$/);
        }
      } finally {
        await deleteAttachments(request, seeded);
      }
    });
  });
});

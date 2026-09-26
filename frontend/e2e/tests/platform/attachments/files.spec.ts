import { randomUUID } from "node:crypto";
import { readFile } from "node:fs/promises";

import type { Page } from "@playwright/test";

import { expect, forEachTheme, test } from "../../../fixtures/test";
import { AttachmentsPage, type UploadFile } from "../../../pages/platform/attachments/files.page";

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

function watchUploads(page: Page): { count: () => number } {
  let uploads = 0;
  page.on("request", (request) => {
    if (request.method() === "POST" && new URL(request.url()).pathname === "/api/platform/attachments")
      uploads += 1;
  });
  return { count: () => uploads };
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

    const uploaded = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        new URL(response.url()).pathname === "/api/platform/attachments",
    );
    await attachments.upload(file);
    expect((await uploaded).status()).toBe(201);
    await expect(attachments.uploadStatus).toContainText(`${file.name} was uploaded.`);
    const row = attachments.row(file.name);
    await expect(row).toBeVisible();
    await expect(row).toContainText("PNG image");
    await expect(row).toContainText("256 bytes");
    await capture("attachments-uploaded");

    const downloadStarted = page.waitForEvent("download");
    await row.getByTestId("attachment-download").click();
    const download = await downloadStarted;
    expect(download.suggestedFilename()).toBe(file.name);
    expect(Buffer.compare(await readFile((await download.path())!), file.buffer)).toBe(0);

    await row.getByTestId("attachment-delete").click();
    await expect(attachments.deleteDialog).toBeVisible();
    await expect(attachments.deleteDialog).toContainText(file.name);
    await capture("attachments-delete-dialog");
    await attachments.cancelDelete.click();
    await expect(attachments.deleteDialog).toHaveCount(0);
    await expect(row).toBeVisible();

    await row.getByTestId("attachment-delete").click();
    await attachments.confirmDelete.click();
    await expect(row).toHaveCount(0);
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
});

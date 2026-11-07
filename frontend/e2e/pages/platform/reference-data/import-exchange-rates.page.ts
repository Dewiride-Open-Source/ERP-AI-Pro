import { expect, type Locator, type Page } from "@playwright/test";
import { readFile } from "node:fs/promises";

import { importExchangeRatesPath } from "./exchange-rates.page";

export type RatesCsv = { readonly name: string; readonly mimeType: string; readonly buffer: Buffer };

export type SavedTemplate = { readonly name: string; readonly content: Buffer };

export const byteOrderMark = "﻿";

export function ratesCsv(name: string, lines: readonly string[], lineBreak = "\r\n"): RatesCsv {
  return { name, mimeType: "text/csv", buffer: Buffer.from(`${lines.join(lineBreak)}${lineBreak}`, "utf8") };
}

function savesDownloadsFromTheTestOrigin(page: Page): boolean {
  return !(process.platform === "win32" && page.context().browser()?.browserType().name() === "webkit");
}

export class ImportExchangeRatesPage {
  readonly heading: Locator;
  readonly chooser: Locator;
  readonly columns: Locator;
  readonly template: Locator;
  readonly dropZone: Locator;
  readonly chooseFile: Locator;
  readonly fileInput: Locator;
  readonly status: Locator;
  readonly refusal: Locator;
  readonly file: Locator;
  readonly problems: Locator;
  readonly rows: Locator;
  readonly form: Locator;
  readonly summary: Locator;
  readonly importButton: Locator;
  readonly outcome: Locator;
  readonly outcomeLink: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("main").getByRole("heading", { name: "Import rates", level: 1 });
    this.chooser = page.getByTestId("exchange-rate-file-chooser");
    this.columns = this.chooser.locator("dl");
    this.template = page.getByTestId("exchange-rate-template");
    this.dropZone = page.getByTestId("file-drop-zone");
    this.chooseFile = page.getByTestId("file-drop-zone-choose");
    this.fileInput = page.getByTestId("file-drop-zone-input");
    this.status = page.getByTestId("exchange-rate-file-status");
    this.refusal = page.getByTestId("exchange-rate-file-refusal");
    this.file = page.getByTestId("exchange-rate-file");
    this.problems = page.getByTestId("exchange-rate-file-problems");
    this.rows = page.getByTestId("exchange-rate-file-rows");
    this.form = page.getByTestId("exchange-rate-import-form");
    this.summary = this.form.locator("[data-slot='alert']");
    this.importButton = page.getByTestId("exchange-rate-import");
    this.outcome = page.getByTestId("exchange-rate-import-outcome");
    this.outcomeLink = this.outcome.getByRole("link", { name: "Open the rates of this file", exact: true });
  }

  async goto(): Promise<void> {
    await this.page.goto(importExchangeRatesPath);
    await expect(this.heading).toBeVisible();
  }

  // The template button and the drop zone work only through React, so they are disabled in the server's HTML and mark
  // themselves as rendering that state until the page hydrates.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.page.getByRole("main").locator("[data-hydrating]")).toHaveCount(0);
  }

  async choose(file: RatesCsv): Promise<void> {
    await expect(this.chooseFile).toBeEnabled();
    await this.fileInput.setInputFiles(file);
  }

  async chooseThroughButton(file: RatesCsv): Promise<void> {
    await expect(this.chooseFile).toBeEnabled();
    const chooser = this.page.waitForEvent("filechooser");
    await this.chooseFile.click();
    await (await chooser).setFiles(file);
  }

  problemTexts(): Promise<string[]> {
    return this.problems.getByRole("listitem").allInnerTexts();
  }

  async downloadTemplate(): Promise<SavedTemplate> {
    if (!savesDownloadsFromTheTestOrigin(this.page)) return this.readSavedTemplate();
    const started = this.page.waitForEvent("download");
    await this.template.click();
    const download = await started;
    return { name: download.suggestedFilename(), content: await readFile(await download.path()) };
  }

  // Playwright's WebKit on Windows starts no download from an origin whose certificate it was told to accept, and the page's
  // content security policy refuses to fetch the address the page made for the file, so there the file the page handed to
  // that address is kept as it is made and read back once the page clicked the link that saves it.
  private async readSavedTemplate(): Promise<SavedTemplate> {
    const saved = await this.page.evaluateHandle(() => {
      const files: { name: string; content: Blob | undefined }[] = [];
      const made: Blob[] = [];
      const createObjectURL = URL.createObjectURL.bind(URL);
      URL.createObjectURL = (object: Blob | MediaSource) => {
        if (object instanceof Blob) made.push(object);
        return createObjectURL(object);
      };
      document.addEventListener(
        "click",
        (event) => {
          if (event.target instanceof HTMLAnchorElement && event.target.hasAttribute("download")) {
            files.push({ name: event.target.download, content: made.at(-1) });
          }
        },
        { capture: true },
      );
      return files;
    });
    await this.template.click();
    await expect
      .poll(() => saved.evaluate((files) => files.length), { message: "files the page saved" })
      .toBe(1);
    const { name, bytes } = await saved.evaluate(async ([file]) => ({
      name: file?.name ?? "",
      bytes: [...new Uint8Array(await (file?.content ?? new Blob()).arrayBuffer())],
    }));
    await saved.dispose();
    return { name, content: Buffer.from(bytes) };
  }
}

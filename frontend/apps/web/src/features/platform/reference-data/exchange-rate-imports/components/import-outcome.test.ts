import assert from "node:assert/strict";
import { test } from "node:test";

import { formFailed, formInvalid, formSucceeded } from "@/shared/forms/state/form-state";

import {
  fileRefusalText,
  fileSummaryText,
  importButtonText,
  importedRatesHref,
  importOutcomeText,
  nothingRecorded,
  withFileLines,
} from "./import-outcome.ts";
import type { RatesFileRow } from "./rates-file.ts";

const basePath = "/platform/reference-data/exchange-rates";

function row(line: number, currency: string, rateDate: string): RatesFileRow {
  return { line, currency, rateDate, rate: "1", unit: "1", source: "" };
}

test("importOutcomeText_RecordedAndUnchanged_SaysHowManyOfEach", () => {
  assert.equal(importOutcomeText(3, 0), "Recorded 3 rates.");
  assert.equal(importOutcomeText(1, 0), "Recorded 1 rate.");
  assert.equal(
    importOutcomeText(2, 1),
    "Recorded 2 rates. 1 rate was already recorded and was left as it was.",
  );
  assert.equal(
    importOutcomeText(0, 1_200),
    "Recorded no new rates. 1,200 rates were already recorded and were left as they were.",
  );
});

test("fileSummaryText_FileWithoutProblems_SaysHowManyRatesAreReady", () => {
  assert.equal(
    fileSummaryText("rates.csv", { rows: [row(2, "USD", "2026-11-07")], problems: [] }),
    "rates.csv holds 1 rate, ready to import.",
  );
});

test("fileSummaryText_FileWithProblems_AsksToCorrectThemInTheFile", () => {
  assert.equal(
    fileSummaryText("rates.csv", {
      rows: [],
      problems: [
        { line: 2, text: "Line 2: the rate is blank." },
        { line: 3, text: "Line 3: the date is blank." },
      ],
    }),
    "rates.csv has 2 problems, so it cannot be imported yet. Correct them in the file, then choose it again.",
  );
  assert.equal(
    fileSummaryText("rates.csv", { rows: [], problems: [{ line: undefined, text: "The file is empty." }] }),
    "rates.csv has 1 problem, so it cannot be imported yet. Correct it in the file, then choose it again.",
  );
});

test("importButtonText_Rates_CountsThem", () => {
  assert.equal(importButtonText(1), "Import 1 rate");
  assert.equal(importButtonText(1_000), "Import 1,000 rates");
});

test("fileRefusalText_EveryRefusal_NamesTheFileAndWhatToDo", () => {
  assert.equal(fileRefusalText("a.csv", "empty"), "a.csv is empty. Choose a CSV file that holds rates.");
  assert.equal(
    fileRefusalText("a.csv", "too-large"),
    "a.csv is larger than 1 MB, more than 1,000 rates need. Split it into smaller files.",
  );
  assert.equal(
    fileRefusalText("a.xlsx", "not-csv"),
    "a.xlsx is not a CSV file. In Excel, save the sheet as CSV UTF-8 (Comma delimited), then choose that file.",
  );
  assert.equal(fileRefusalText("a.xlsx", "unsupported-type"), fileRefusalText("a.xlsx", "not-csv"));
  assert.equal(
    fileRefusalText("a.csv", "not-utf8"),
    "a.csv is not saved as UTF-8 text. In Excel, save it as CSV UTF-8 (Comma delimited), then choose it again.",
  );
  assert.equal(fileRefusalText("a.csv", "unreadable"), "a.csv could not be read. Choose it again.");
});

test("withFileLines_RefusedRates_AreNamedByTheirLinesInLineOrder", () => {
  const answer = formInvalid({
    fieldErrors: {
      "rates.3.rate": [
        "USD for 07-11-2026 is already recorded as 88.69 rupees for 1; correct that rate instead.",
      ],
      "rates.0.currency": ["'XYZ' is not the code of an ISO 4217 currency such as USD."],
    },
  });

  assert.deepEqual(withFileLines(answer, [2, 3, 5, 6]), {
    status: "invalid",
    fieldErrors: {},
    formErrors: [
      nothingRecorded,
      "Line 2: 'XYZ' is not the code of an ISO 4217 currency such as USD.",
      "Line 6: USD for 07-11-2026 is already recorded as 88.69 rupees for 1; correct that rate instead.",
    ],
  });
});

test("withFileLines_ProblemOfTheWholeRequestOrAnUnknownRow_StaysAsTheApiWordedIt", () => {
  const answer = formInvalid({
    fieldErrors: { rates: ["Too many rates."], "rates.9.rate": ["Refused."] },
    formErrors: ["Something else."],
  });

  assert.deepEqual(withFileLines(answer, [2]), {
    status: "invalid",
    fieldErrors: {},
    formErrors: [nothingRecorded, "Something else.", "Too many rates.", "Refused."],
  });
});

test("withFileLines_AnswerThatIsNoRefusalOfRates_IsKept", () => {
  const failed = formFailed("Someone recorded a rate meanwhile.", { code: "concurrency.conflict" });
  const succeeded = formSucceeded();

  assert.equal(withFileLines(failed, [2]), failed);
  assert.equal(withFileLines(succeeded, [2]), succeeded);
});

test("importedRatesHref_FileRows_FindTheirCurrenciesBetweenTheirFirstAndLastDay", () => {
  assert.equal(
    importedRatesHref(basePath, [
      row(2, "USD", "2026-11-07"),
      row(3, "CHF", "2026-11-01"),
      row(4, "USD", "2026-11-03"),
    ]),
    `${basePath}?currency=CHF&currency=USD&dayFrom=2026-11-01&dayTo=2026-11-07`,
  );
});

import assert from "node:assert/strict";
import { test } from "node:test";

import { readRatesFile, ratesTemplate, type RatesFileRules } from "./rates-file.ts";

const rules: RatesFileRules = { today: "2026-11-07", currencies: new Set(["CHF", "JPY", "USD"]) };

function problemsOf(text: string, given: RatesFileRules = rules): string[] {
  return readRatesFile(text, given).problems.map((problem) => problem.text);
}

test("readRatesFile_FileExcelSaves_ReadsEveryRateWithTheLineItStartsOn", () => {
  const file = readRatesFile(
    "﻿date,currency,unit,rate,source\r\n" +
      '07-11-2026,USD,1,88.6900,"FBIL reference rate, 1:30 PM"\r\n' +
      "06/11/2026,jpy,100,56.1234,\r\n",
    rules,
  );

  assert.deepEqual(file.problems, []);
  assert.deepEqual(file.rows, [
    {
      line: 2,
      currency: "USD",
      rateDate: "2026-11-07",
      rate: "88.6900",
      unit: "1",
      source: "FBIL reference rate, 1:30 PM",
    },
    { line: 3, currency: "JPY", rateDate: "2026-11-06", rate: "56.1234", unit: "100", source: "" },
  ]);
});

test("readRatesFile_ColumnsInAnyOrderAndCaseWithoutSource_AreRead", () => {
  const file = readRatesFile(" Rate ,CURRENCY,Unit,Date\n83.25,chf,1,2026-03-31\n", rules);

  assert.deepEqual(file.problems, []);
  assert.deepEqual(file.rows, [
    { line: 2, currency: "CHF", rateDate: "2026-03-31", rate: "83.25", unit: "1", source: "" },
  ]);
});

test("readRatesFile_LineBreaksOfEveryKind_CountLinesAsAnEditorShowsThem", () => {
  const file = readRatesFile(
    "date,currency,unit,rate\r\n01-04-2026,USD,1,85\n\n02-04-2026,USD,1,86\r03-04-2026,USD,1,87",
    rules,
  );

  assert.deepEqual(
    file.rows.map((row) => [row.line, row.rateDate]),
    [
      [2, "2026-04-01"],
      [4, "2026-04-02"],
      [5, "2026-04-03"],
    ],
  );
});

test("readRatesFile_QuotedCellWithALineBreakAndADoubledQuote_KeepsBothAndNamesTheLineItStartsOn", () => {
  const file = readRatesFile(
    'date,currency,unit,rate,source\n07-11-2026,USD,1,88.69,"Desk ""A""\nnoon"\n06-11-2026,USD,1,88.5,\n',
    rules,
  );

  assert.deepEqual(file.problems, []);
  assert.deepEqual(
    file.rows.map((row) => [row.line, row.source]),
    [
      [2, 'Desk "A"\nnoon'],
      [4, ""],
    ],
  );
});

test("readRatesFile_RatesWithGroupingOrTheRupeeSign_AreReadAsFigures", () => {
  const file = readRatesFile('date,currency,unit,rate\n07-11-2026,USD,"1,000","₹1,23,456.5"\n', rules);

  assert.deepEqual(file.problems, []);
  assert.equal(file.rows[0]?.rate, "123456.5");
  assert.equal(file.rows[0]?.unit, "1000");
});

test("readRatesFile_BlankAndMalformedCells_AreEachReportedByLine", () => {
  assert.deepEqual(
    problemsOf(
      "date,currency,unit,rate,source\n" +
        ",,,,\n" +
        "07-11-2026,,1,,\n" +
        "2026/31/03,US,5,8x.5,\n" +
        "07-11-2026,INR,1,83.12345,\n" +
        "07-11-2026,XYZ,1,0,\n" +
        "07-11-2026,USD,1,-5,\n" +
        "07-11-2026,USD,1,1000000.0001,\n" +
        `07-11-2026,USD,1,1,${"x".repeat(201)}\n`,
    ),
    [
      "Line 3: the currency is blank.",
      "Line 3: the rate is blank.",
      'Line 4: the date "2026/31/03" is not a day written as 31-03-2026 or 2026-03-31.',
      'Line 4: the currency "US" is not a three-letter code such as USD.',
      'Line 4: the unit "5" is not 1, 10, 100, 1,000, 10,000 or 1,00,000.',
      'Line 4: the rate "8x.5" is not a number such as 83.2475.',
      "Line 5: the currency is INR, and rates are recorded for foreign currencies only.",
      "Line 5: the rate 83.12345 has more than 4 digits after the decimal point.",
      "Line 6: the currency XYZ is not one the ERP knows.",
      "Line 6: the rate 0 is not more than 0.",
      "Line 7: the rate -5 is not more than 0.",
      "Line 8: the rate 1000000.0001 is more than 10,00,000.",
      "Line 9: the source is longer than 200 characters.",
    ],
  );
});

test("readRatesFile_DayAfterToday_IsRefusedAndTodayIsRead", () => {
  const file = readRatesFile("date,currency,unit,rate\n08-11-2026,USD,1,88\n07-11-2026,USD,1,88\n", rules);

  assert.deepEqual(
    file.problems.map((problem) => problem.text),
    ["Line 2: the date 08-11-2026 is after today."],
  );
  assert.deepEqual(
    file.rows.map((row) => row.rateDate),
    ["2026-11-07"],
  );
});

test("readRatesFile_WithoutAListOfCurrencies_LeavesTheCodeToTheErp", () => {
  assert.deepEqual(problemsOf("date,currency,unit,rate\n07-11-2026,XYZ,1,1\n", { today: rules.today }), []);
});

test("readRatesFile_SameCurrencyAndDayTwice_PointsToTheFirstLine", () => {
  assert.deepEqual(
    problemsOf("date,currency,unit,rate\n07-11-2026,USD,1,88\n06-11-2026,USD,1,88\n2026-11-07,usd,1,89\n"),
    ["Line 4: USD for 07-11-2026 is already on line 2."],
  );
});

test("readRatesFile_UnknownMissingOrRepeatedColumns_AreRefusedOnTheHeaderLine", () => {
  assert.deepEqual(problemsOf("date,currency,amount,rate,date\n07-11-2026,USD,1,88,07-11-2026\n"), [
    'Line 1: "amount" is not a column the import knows; the columns are date, currency, unit, rate and source.',
    "Line 1: the column date appears twice.",
    "Line 1: the header line names no unit column.",
  ]);
});

test("readRatesFile_CellOutsideTheNamedColumns_IsRefusedAndBlankOnesAreIgnored", () => {
  assert.deepEqual(
    problemsOf("date,currency,unit,rate,\n07-11-2026,USD,1,88,\n06-11-2026,USD,1,88,extra,more\n"),
    ["Line 3: a cell sits outside the columns the header line names."],
  );
});

test("readRatesFile_BrokenQuotes_AreRefusedOnTheLineTheyStartOn", () => {
  assert.deepEqual(
    problemsOf(
      'date,currency,unit,rate,source\n07-11-2026,USD,1,88,ab"c\n06-11-2026,USD,1,88,"FBIL" noon\n05-11-2026,USD,1,88,"open\n',
    ),
    [
      "Line 2: a cell holds a double quote but does not start with one; put the whole cell in double quotes and write the quote twice.",
      "Line 3: a cell in double quotes has more text after its closing quote.",
      "Line 4: a cell opens a double quote that is never closed.",
    ],
  );
});

test("readRatesFile_EmptyFileOrHeaderAlone_HoldsNoRates", () => {
  assert.deepEqual(problemsOf(""), ["The file is empty."]);
  assert.deepEqual(problemsOf("﻿\r\n"), ["The file is empty."]);
  assert.deepEqual(problemsOf("date,currency,unit,rate\r\n"), [
    "The file holds no rates under its header line.",
  ]);
});

test("readRatesFile_MoreThanAnImportTakes_AsksToSplitTheFile", () => {
  const lines = Array.from(
    { length: 1_001 },
    (_, index) => `${String((index % 28) + 1).padStart(2, "0")}-01-1990,USD,1,1`,
  );

  assert.deepEqual(problemsOf(`date,currency,unit,rate\n${lines.join("\n")}\n`), [
    "The file holds 1,001 rates, and an import takes at most 1,000. Split it into smaller files.",
  ]);
});

test("readRatesFile_TemplateAsDownloaded_RefusesItsExampleRow", () => {
  assert.deepEqual(problemsOf(ratesTemplate), [
    "Line 2: this is the template's example row; replace it with a rate of your own.",
  ]);
});

test("ratesTemplate_Text_IsAHeaderAndOneExampleRowAsExcelReadsThem", () => {
  assert.equal(
    ratesTemplate,
    "﻿date,currency,unit,rate,source\r\n31-03-2026,USD,1,85.0000,Example: replace this row with your own rates\r\n",
  );
});

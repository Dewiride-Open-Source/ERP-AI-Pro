import { compareIsoDates, formatDisplayDate, parseDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";
import { groupIndian, normaliseAmount } from "@dewiride/erp-ui/lib/indian-number";

import {
  currencyCodePattern,
  exchangeRateUnits,
  indianRupee,
  maxImportedRates,
  maxRate,
  rateScale,
  sourceMaxLength,
} from "../../exchange-rates/forms/exchange-rate.schema.ts";

export const ratesFileColumns = ["date", "currency", "unit", "rate", "source"] as const;

export type RatesFileColumn = (typeof ratesFileColumns)[number];

export interface RatesFileRow {
  readonly line: number;
  readonly currency: string;
  readonly rateDate: string;
  readonly rate: string;
  readonly unit: string;
  readonly source: string;
}

export interface RatesFileProblem {
  readonly line: number | undefined;
  readonly text: string;
}

export interface RatesFile {
  readonly rows: readonly RatesFileRow[];
  readonly problems: readonly RatesFileProblem[];
}

export interface RatesFileRules {
  readonly today: string;
  readonly currencies?: ReadonlySet<string> | undefined;
}

export const maxRatesFileBytes = 1_048_576;

export const ratesTemplateName = "exchange-rates-template.csv";

export const ratesTemplateExampleSource = "Example: replace this row with your own rates";

// Excel reads a file as UTF-8 only when it starts with a byte order mark, and RFC 4180 ends each line with CRLF.
export const ratesTemplate = `﻿date,currency,unit,rate,source\r\n31-03-2026,USD,1,85.0000,${ratesTemplateExampleSource}\r\n`;

interface FileRecord {
  readonly line: number;
  readonly cells: readonly string[];
  readonly fault: string | undefined;
}

type CellReading = { readonly value: string } | { readonly problem: string };

const requiredColumns: readonly RatesFileColumn[] = ["date", "currency", "unit", "rate"];

const byteOrderMark = "﻿";

const unitTexts: ReadonlySet<string> = new Set(exchangeRateUnits.map(String));

export const unitChoices = `${exchangeRateUnits
  .slice(0, -1)
  .map((unit) => groupIndian(String(unit), 0))
  .join(", ")} or ${groupIndian(String(exchangeRateUnits.at(-1)), 0)}`;

const rateFigures = /^(-?)(\d*)(?:\.(\d*))?$/;

const amountDecoration = /[\s,₹]/g;

const leadingZeros = /^0+/;

const counts = new Intl.NumberFormat("en-IN");

const shownCellLength = 40;

// A CSV file is read as RFC 4180 writes it (cells split by commas, a cell in double quotes may hold commas, line breaks and a
// doubled quote), with a line ending in CRLF, LF or CR alike; a rate is named by the line of the file it starts on.
export function readRatesFile(text: string, rules: RatesFileRules): RatesFile {
  const records = fileRecords(text.startsWith(byteOrderMark) ? text.slice(1) : text).filter(
    (record) => !isBlank(record),
  );
  const [header, ...data] = records;
  if (header === undefined) return refused({ line: undefined, text: "The file is empty." });
  if (header.fault !== undefined) return refused(lineProblem(header.line, header.fault));

  const { columns, problems: headerProblems } = headerColumns(header);
  if (headerProblems.length > 0) return { rows: [], problems: headerProblems };
  if (data.length === 0) {
    return refused({ line: undefined, text: "The file holds no rates under its header line." });
  }
  if (data.length > maxImportedRates) {
    return refused({
      line: undefined,
      text: `The file holds ${counts.format(data.length)} rates, and an import takes at most ${counts.format(maxImportedRates)}. Split it into smaller files.`,
    });
  }

  const rows: RatesFileRow[] = [];
  const problems: RatesFileProblem[] = [];
  const firstLines = new Map<string, number>();
  for (const record of data) {
    if (record.fault !== undefined) {
      problems.push(lineProblem(record.line, record.fault));
      continue;
    }
    const read = readRow(record, columns, rules);
    if ("problems" in read) {
      problems.push(...read.problems);
      continue;
    }
    const key = `${read.currency} ${read.rateDate}`;
    const first = firstLines.get(key);
    if (first !== undefined) {
      problems.push(
        lineProblem(
          record.line,
          `${read.currency} for ${formatDisplayDate(read.rateDate)} is already on line ${first}`,
        ),
      );
      continue;
    }
    firstLines.set(key, record.line);
    rows.push(read);
  }
  return { rows, problems };
}

function refused(problem: RatesFileProblem): RatesFile {
  return { rows: [], problems: [problem] };
}

function lineProblem(line: number, problem: string): RatesFileProblem {
  return { line, text: `Line ${line}: ${problem}.` };
}

function lineBreakLength(text: string, index: number): number {
  const character = text.charAt(index);
  if (character === "\r") return text.charAt(index + 1) === "\n" ? 2 : 1;
  return character === "\n" ? 1 : 0;
}

function fileRecords(text: string): readonly FileRecord[] {
  const reader = new RecordReader(text);
  for (let index = 0; index < text.length; index += 1) {
    index += reader.quoting ? reader.readQuoted(index) : reader.readPlain(index);
  }
  return reader.finish();
}

// Each read takes the character at an index and answers how many more characters it took with it: the second quote of a
// doubled quote, or the line feed of a CRLF.
class RecordReader {
  quoting = false;
  private readonly text: string;
  private readonly records: FileRecord[] = [];
  private cells: string[] = [];
  private cell = "";
  private line = 1;
  private recordLine = 1;
  private closedQuote = false;
  private fault: string | undefined;

  constructor(text: string) {
    this.text = text;
  }

  readQuoted(index: number): number {
    const character = this.text.charAt(index);
    const lineBreak = lineBreakLength(this.text, index);
    if (character === '"' && this.text.charAt(index + 1) === '"') {
      this.cell += '"';
      return 1;
    }
    if (character === '"') {
      this.quoting = false;
      this.closedQuote = true;
    } else if (lineBreak > 0) {
      this.cell += "\n";
      this.line += 1;
      return lineBreak - 1;
    } else {
      this.cell += character;
    }
    return 0;
  }

  readPlain(index: number): number {
    const character = this.text.charAt(index);
    const lineBreak = lineBreakLength(this.text, index);
    if (lineBreak > 0) {
      this.endRecord();
      this.line += 1;
      this.recordLine = this.line;
      return lineBreak - 1;
    }
    if (character === ",") this.endCell();
    else if (character === '"') this.readQuoteMark();
    else if (!this.closedQuote) this.cell += character;
    else if (character.trim() !== "")
      this.fault ??= "a cell in double quotes has more text after its closing quote";
    return 0;
  }

  finish(): readonly FileRecord[] {
    if (this.quoting) this.fault ??= "a cell opens a double quote that is never closed";
    const started = this.cell !== "" || this.cells.length > 0 || this.quoting || this.closedQuote;
    if (started || this.fault !== undefined) this.endRecord();
    return this.records;
  }

  private readQuoteMark(): void {
    if (this.cell.trim() === "" && !this.closedQuote) {
      this.quoting = true;
      this.cell = "";
      return;
    }
    this.fault ??=
      "a cell holds a double quote but does not start with one; put the whole cell in double quotes and write the quote twice";
  }

  private endCell(): void {
    this.cells.push(this.cell);
    this.cell = "";
    this.closedQuote = false;
  }

  private endRecord(): void {
    this.endCell();
    this.records.push({ line: this.recordLine, cells: this.cells, fault: this.fault });
    this.cells = [];
    this.fault = undefined;
  }
}

function isBlank(record: FileRecord): boolean {
  return record.fault === undefined && record.cells.every((cell) => cell.trim() === "");
}

function isColumn(name: string): name is RatesFileColumn {
  return (ratesFileColumns as readonly string[]).includes(name);
}

function headerColumns(header: FileRecord): {
  readonly columns: readonly (RatesFileColumn | undefined)[];
  readonly problems: readonly RatesFileProblem[];
} {
  const columns: (RatesFileColumn | undefined)[] = [];
  const problems: RatesFileProblem[] = [];
  const named = new Set<RatesFileColumn>();
  for (const cell of header.cells) {
    const name = cell.trim().toLowerCase();
    if (name !== "" && !isColumn(name)) {
      problems.push(
        lineProblem(
          header.line,
          `${shown(cell.trim())} is not a column the import knows; the columns are date, currency, unit, rate and source`,
        ),
      );
    } else if (isColumn(name) && named.has(name)) {
      problems.push(lineProblem(header.line, `the column ${name} appears twice`));
    } else if (isColumn(name)) {
      named.add(name);
      columns.push(name);
      continue;
    }
    columns.push(undefined);
  }
  for (const column of requiredColumns) {
    if (!named.has(column))
      problems.push(lineProblem(header.line, `the header line names no ${column} column`));
  }
  return { columns, problems };
}

function readRow(
  record: FileRecord,
  columns: readonly (RatesFileColumn | undefined)[],
  rules: RatesFileRules,
): RatesFileRow | { readonly problems: readonly RatesFileProblem[] } {
  const cells = new Map<RatesFileColumn, string>();
  const problems: RatesFileProblem[] = [];
  record.cells.forEach((cell, index) => {
    const column = columns[index];
    if (column !== undefined) cells.set(column, cell.trim());
    else if (cell.trim() !== "" && !problems.some((problem) => problem.line === record.line)) {
      problems.push(lineProblem(record.line, "a cell sits outside the columns the header line names"));
    }
  });

  const date = readDate(cells.get("date") ?? "", rules.today);
  const currency = readCurrency(cells.get("currency") ?? "", rules.currencies);
  const unit = readUnit(cells.get("unit") ?? "");
  const rate = readRate(cells.get("rate") ?? "");
  const source = readSource(cells.get("source") ?? "");
  for (const reading of [date, currency, unit, rate, source]) {
    if ("problem" in reading) problems.push(lineProblem(record.line, reading.problem));
  }
  if (
    problems.length === 0 &&
    "value" in date &&
    "value" in currency &&
    "value" in unit &&
    "value" in rate &&
    "value" in source
  ) {
    return {
      line: record.line,
      currency: currency.value,
      rateDate: date.value,
      rate: rate.value,
      unit: unit.value,
      source: source.value,
    };
  }
  return { problems };
}

function shown(cell: string): string {
  const text = cell.length > shownCellLength ? `${cell.slice(0, shownCellLength)}…` : cell;
  return `"${text}"`;
}

function readDate(cell: string, today: string): CellReading {
  if (cell === "") return { problem: "the date is blank" };
  const isoDate = parseDisplayDate(cell);
  if (isoDate === null) {
    return { problem: `the date ${shown(cell)} is not a day written as 31-03-2026 or 2026-03-31` };
  }
  if (compareIsoDates(isoDate, today) > 0)
    return { problem: `the date ${formatDisplayDate(isoDate)} is after today` };
  return { value: isoDate };
}

function readCurrency(cell: string, known: ReadonlySet<string> | undefined): CellReading {
  if (cell === "") return { problem: "the currency is blank" };
  const code = cell.toUpperCase();
  if (!currencyCodePattern.test(code)) {
    return { problem: `the currency ${shown(cell)} is not a three-letter code such as USD` };
  }
  if (code === indianRupee) {
    return { problem: "the currency is INR, and rates are recorded for foreign currencies only" };
  }
  if (known !== undefined && !known.has(code))
    return { problem: `the currency ${code} is not one the ERP knows` };
  return { value: code };
}

function readUnit(cell: string): CellReading {
  if (cell === "") return { problem: "the unit is blank" };
  const unit = cell.replace(/[\s,]/g, "");
  return unitTexts.has(unit) ? { value: unit } : { problem: `the unit ${shown(cell)} is not ${unitChoices}` };
}

function readRate(cell: string): CellReading {
  if (cell === "") return { problem: "the rate is blank" };
  const figures = rateFigures.exec(cell.replace(amountDecoration, ""));
  const [, sign = "", integer = "", fraction = ""] = figures ?? [];
  if (figures === null || (integer === "" && fraction === "")) {
    return { problem: `the rate ${shown(cell)} is not a number such as 83.2475` };
  }
  if (fraction.length > rateScale) {
    return { problem: `the rate ${cell} has more than ${rateScale} digits after the decimal point` };
  }
  const notPositive = `the rate ${cell} is not more than 0`;
  const tooLarge = `the rate ${cell} is more than ${groupIndian(maxRate, 0)}`;
  if (sign !== "") return { problem: notPositive };
  if (integer.replace(leadingZeros, "").length > maxRate.length) return { problem: tooLarge };

  const normalised = normaliseAmount(`${integer}.${fraction}`, { scale: rateScale, allowNegative: false });
  const canonical = "canonical" in normalised ? normalised.canonical : "";
  const value = Number(canonical);
  if (canonical === "" || Number.isNaN(value)) {
    return { problem: `the rate ${shown(cell)} is not a number such as 83.2475` };
  }
  if (value === 0) return { problem: notPositive };
  if (value > Number(maxRate)) return { problem: tooLarge };
  return { value: canonical };
}

function readSource(cell: string): CellReading {
  if (cell === ratesTemplateExampleSource) {
    return { problem: "this is the template's example row; replace it with a rate of your own" };
  }
  if (cell.length > sourceMaxLength) {
    return { problem: `the source is longer than ${sourceMaxLength} characters` };
  }
  return { value: cell };
}

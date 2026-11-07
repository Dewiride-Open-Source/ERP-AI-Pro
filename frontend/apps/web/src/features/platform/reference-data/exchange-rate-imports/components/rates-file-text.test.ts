import assert from "node:assert/strict";
import { test } from "node:test";

import { readFileText } from "./rates-file-text.ts";

const header = "date,currency,unit,rate,source\r\n";

function bytesOf(...parts: readonly (string | readonly number[])[]): () => Promise<ArrayBuffer> {
  const encoded = parts.flatMap((part) =>
    typeof part === "string" ? [...new TextEncoder().encode(part)] : part,
  );
  return () => Promise.resolve(new Uint8Array(encoded).buffer);
}

test("readFileText_CsvOrTextNameInAnyCase_IsRead", async () => {
  for (const name of ["rates.csv", "RATES.CSV", "rates.Csv", "rates.txt", "Rates.TXT"]) {
    assert.deepEqual(await readFileText(name, bytesOf(header)), { text: header }, name);
  }
});

test("readFileText_NameOfAnotherKindOfFile_IsRefusedWithoutReadingIt", async () => {
  let reads = 0;
  const counted = () => {
    reads += 1;
    return Promise.resolve(new ArrayBuffer(0));
  };

  for (const name of ["rates.xlsx", "rates.xls", "rates.csv.xlsx", "rates", "rates.csvx"]) {
    assert.deepEqual(await readFileText(name, counted), { refusal: "not-csv" }, name);
  }
  assert.equal(reads, 0);
});

test("readFileText_ByteOrderMark_IsLeftOutOfTheText", async () => {
  assert.deepEqual(await readFileText("rates.csv", bytesOf([0xef, 0xbb, 0xbf], header)), { text: header });
});

test("readFileText_TextThatIsNotUtf8_IsRefused", async () => {
  const windows1252 = bytesOf(header, "07-11-2026,CHF,1,95.1234,Z", [0xfc], "rich desk\r\n");

  assert.deepEqual(await readFileText("rates.csv", windows1252), { refusal: "not-utf8" });
  assert.deepEqual(await readFileText("rates.csv", bytesOf([0xc3])), { refusal: "not-utf8" });
});

test("readFileText_Utf8BeyondAscii_IsReadAsWritten", async () => {
  const line = "07-11-2026,CHF,1,95.1234,Zürich desk, ₹ rates\r\n";

  assert.deepEqual(await readFileText("rates.csv", bytesOf(header, line)), { text: `${header}${line}` });
});

test("readFileText_FileTheBrowserCannotRead_IsRefusedAsUnreadable", async () => {
  const gone = () => Promise.reject(new DOMException("The file was moved.", "NotFoundError"));

  assert.deepEqual(await readFileText("rates.csv", gone), { refusal: "unreadable" });
});

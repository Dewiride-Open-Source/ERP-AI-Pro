import assert from "node:assert/strict";
import { test } from "node:test";

import type { NumberedDocumentResponse, NumberSeriesYearResponse } from "@dewiride/erp-api-client";

import { numberedDocumentOf } from "./numbering-items.ts";

const seriesId = "0192f3c4-5b6a-7d8e-9f01-23456789abcd";

function withoutMember<T extends object>(value: T, member: keyof T): T {
  return Object.fromEntries(Object.entries(value).filter(([key]) => key !== member)) as T;
}

function year(values: Partial<NumberSeriesYearResponse> = {}): NumberSeriesYearResponse {
  return {
    year: "2026-27",
    format: { prefix: "TI.", year: "abbreviated", separator: ".", digits: 3 },
    maxLength: 14,
    firstNumber: 1,
    nextNumber: 43,
    lastNumber: 99_999,
    inUse: true,
    firstText: "TI.26-27.001",
    nextText: "TI.26-27.043",
    lastText: "TI.26-27.99999",
    lastIssuedText: "TI.26-27.042",
    ...values,
  };
}

function document(values: Partial<NumberedDocumentResponse> = {}): NumberedDocumentResponse {
  return {
    documentType: "test.numbering.invoices-01",
    title: "Test invoices 01",
    description: "Invoices of a test.",
    currentYear: "2026-27",
    rule: { maxLength: 14, specialCharacters: "-." },
    series: {
      id: seriesId,
      format: { prefix: "TI.", year: "abbreviated", separator: ".", digits: 3 },
      version: "AAAAAAAAB9E=",
      years: [year()],
    },
    ...values,
  };
}

test("numberedDocumentOf_ANumberingThatIsSetUp_KeepsItsFormatVersionAndYears", () => {
  assert.deepEqual(numberedDocumentOf(document()), {
    documentType: "test.numbering.invoices-01",
    title: "Test invoices 01",
    description: "Invoices of a test.",
    currentYear: "2026-27",
    rule: { maxLength: 14, specialCharacters: "-." },
    series: {
      id: seriesId,
      format: { prefix: "TI.", year: "abbreviated", separator: ".", digits: 3 },
      version: "AAAAAAAAB9E=",
      years: [
        {
          year: "2026-27",
          format: { prefix: "TI.", year: "abbreviated", separator: ".", digits: 3 },
          maxLength: 14,
          inUse: true,
          firstText: "TI.26-27.001",
          nextText: "TI.26-27.043",
          lastText: "TI.26-27.99999",
          lastIssuedText: "TI.26-27.042",
        },
      ],
    },
  });
});

test("numberedDocumentOf_TheApisNullsAsKiotaReadsThem_AreAbsent", () => {
  const read = numberedDocumentOf(document({ rule: {}, series: {} }));

  assert.equal(read.rule, null);
  assert.equal(read.series, null);
  assert.equal(numberedDocumentOf(withoutMember(document({ rule: null }), "series")).series, null);
});

test("numberedDocumentOf_AYearThatHasGivenNoNumberOrEveryNumber_KeepsTheMissingTextsAsNull", () => {
  const fresh = numberedDocumentOf(
    document({
      series: {
        id: seriesId,
        format: { prefix: "", year: "none", separator: "", digits: 1 },
        version: "AAAAAAAAB9E=",
        years: [
          withoutMember(year({ inUse: false }), "lastIssuedText"),
          year({ year: "2025-26", nextText: null }),
        ],
      },
    }),
  );

  assert.deepEqual(
    fresh.series?.years.map(({ nextText, lastIssuedText }) => ({ nextText, lastIssuedText })),
    [
      { nextText: "TI.26-27.043", lastIssuedText: null },
      { nextText: null, lastIssuedText: "TI.26-27.042" },
    ],
  );
  assert.deepEqual(fresh.series?.format, { prefix: "", year: "none", separator: "", digits: 1 });
});

test("numberedDocumentOf_AnAnswerWithoutAMemberTheContractNames_Throws", () => {
  const incomplete: readonly NumberedDocumentResponse[] = [
    withoutMember(document(), "title"),
    document({ title: "" }),
    document({ currentYear: null }),
    document({ rule: { specialCharacters: "-." } }),
    document({
      series: { id: seriesId, format: {}, version: "AAAAAAAAB9E=", years: [] },
    }),
    document({
      series: {
        id: seriesId,
        format: { prefix: "TI", year: "monthly" as "full", separator: "", digits: 3 },
        version: "AAAAAAAAB9E=",
        years: [],
      },
    }),
    document({
      series: {
        id: seriesId,
        format: { prefix: "TI", year: "none", separator: "", digits: 3 },
        version: "",
        years: [],
      },
    }),
    document({
      series: {
        id: seriesId,
        format: { prefix: "TI", year: "none", separator: "", digits: 3 },
        version: "AAAAAAAAB9E=",
        years: [withoutMember(year(), "firstText")],
      },
    }),
    document({
      series: {
        id: seriesId,
        format: { prefix: "TI", year: "none", separator: "", digits: 3 },
        version: "AAAAAAAAB9E=",
        years: [year({ nextText: "TI.26-27." })],
      },
    }),
  ];

  for (const answer of incomplete) assert.throws(() => numberedDocumentOf(answer), TypeError);
});

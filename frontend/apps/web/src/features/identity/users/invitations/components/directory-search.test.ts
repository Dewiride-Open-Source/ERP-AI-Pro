import assert from "node:assert/strict";
import { test } from "node:test";

import {
  directoryAccessDenied,
  directoryFailure,
  directoryMatches,
  directorySearchMaxLength,
  directoryTimedOut,
  directoryUnavailable,
  nobodyMatches,
  readSearchText,
  searchHasAmpersand,
  searchHasHiddenCharacter,
  searching,
  searchSummary,
  searchTooLong,
  searchTooShort,
  type DirectoryMatch,
} from "./directory-search.ts";

const asha: DirectoryMatch = {
  entraObjectId: "4a1d6d4e-5c3f-4b0e-9a41-0d6f0c2b7e11",
  displayName: "Asha Rao",
  signInName: "asha.rao@contoso.example",
};

const kiran: DirectoryMatch = {
  entraObjectId: "8f0c1a2b-3d4e-4f60-8a7b-9c0d1e2f3a4b",
  displayName: "Kiran Das",
  signInName: "kiran.das@contoso.example",
  personId: "019a3c5e-7d1f-7a2b-9c3d-4e5f6a7b8c9d",
};

test("readSearchText_BlankText_IsEmpty", () => {
  for (const text of ["", "   ", "\t "]) {
    assert.deepEqual(readSearchText(text), { status: "empty" }, JSON.stringify(text));
  }
});

test("readSearchText_OneCharacterBesidesSpaces_IsTooShortToSend", () => {
  for (const text of ["a", " a ", "\ta"]) {
    assert.deepEqual(readSearchText(text), { status: "short" }, JSON.stringify(text));
  }
});

test("readSearchText_TextTheApiAccepts_IsSentTrimmed", () => {
  assert.deepEqual(readSearchText("as"), { status: "ready", text: "as" });
  assert.deepEqual(readSearchText("  Asha Rao \t"), { status: "ready", text: "Asha Rao" });
  assert.deepEqual(readSearchText(` ${"a".repeat(directorySearchMaxLength)} `), {
    status: "ready",
    text: "a".repeat(directorySearchMaxLength),
  });
});

test("readSearchText_TextTheApiRefuses_IsRefusedWithItsReason", () => {
  const refusals: readonly (readonly [string, string])[] = [
    ["R&D", searchHasAmpersand],
    ["&", searchHasAmpersand],
    ["asha\trao", searchHasHiddenCharacter],
    ["asha\u0000", searchHasHiddenCharacter],
    ["asha\u0085rao", searchHasHiddenCharacter],
    ["a".repeat(directorySearchMaxLength + 1), searchTooLong],
  ];
  for (const [text, message] of refusals) {
    assert.deepEqual(readSearchText(text), { status: "refused", message }, JSON.stringify(text));
  }
});

test("searchSummary_TextTooShort_AsksForMoreCharacters", () => {
  assert.equal(searchSummary({ status: "short" }, { loading: false, answer: undefined }), searchTooShort);
});

test("searchSummary_EmptyOrRefusedText_SaysNothing", () => {
  assert.equal(searchSummary({ status: "empty" }, { loading: false, answer: undefined }), "");
  assert.equal(
    searchSummary({ status: "refused", message: searchHasAmpersand }, { loading: false, answer: undefined }),
    "",
  );
});

test("searchSummary_SearchRunning_SaysItIsSearchingOverTheLastAnswer", () => {
  assert.equal(
    searchSummary(
      { status: "ready", text: "as" },
      { loading: true, answer: { people: [asha], hasMore: false } },
    ),
    searching,
  );
});

test("searchSummary_AnswerArrived_CountsThePeopleFound", () => {
  const ready = { status: "ready", text: "as" } as const;

  assert.equal(
    searchSummary(ready, { loading: false, answer: { people: [], hasMore: false } }),
    nobodyMatches,
  );
  assert.equal(
    searchSummary(ready, { loading: false, answer: { people: [asha], hasMore: false } }),
    "1 person found.",
  );
  assert.equal(
    searchSummary(ready, { loading: false, answer: { people: [asha, kiran], hasMore: false } }),
    "2 people found.",
  );
  assert.equal(
    searchSummary(ready, { loading: false, answer: { people: [asha, kiran], hasMore: true } }),
    "2 people found. More people match; type more of the name.",
  );
});

test("searchSummary_SearchFailed_LeavesTheFailureToItsAlert", () => {
  assert.equal(
    searchSummary(
      { status: "ready", text: "as" },
      { loading: false, answer: { error: directoryUnavailable } },
    ),
    "",
  );
});

test("directoryMatches_PeopleOfTheAnswer_KeepTheirIdsNamesAndRecord", () => {
  assert.deepEqual(
    directoryMatches([
      {
        entraObjectId: asha.entraObjectId,
        displayName: asha.displayName,
        signInName: asha.signInName,
        userPrincipalName: asha.signInName,
        mail: null,
        personId: null,
      },
      {
        entraObjectId: kiran.entraObjectId,
        displayName: kiran.displayName,
        signInName: kiran.signInName,
        userPrincipalName: kiran.signInName,
        mail: kiran.signInName,
        personId: kiran.personId ?? null,
      },
    ]),
    [
      { ...asha, personId: undefined },
      { ...kiran, personId: kiran.personId },
    ],
  );
});

test("directoryMatches_PersonWithoutIdOrNames_IsLeftOut", () => {
  assert.deepEqual(
    directoryMatches([
      { displayName: asha.displayName, signInName: asha.signInName },
      { entraObjectId: asha.entraObjectId, signInName: asha.signInName },
      { entraObjectId: asha.entraObjectId, displayName: asha.displayName },
      { entraObjectId: asha.entraObjectId, displayName: "", signInName: asha.signInName },
    ]),
    [],
  );
});

test("directoryFailure_EntraRefusesTheDirectory_AsksToSignInAgainAndKeepsTheCode", () => {
  assert.deepEqual(
    directoryFailure({
      status: 403,
      problem: { status: 403, code: "directory.access-denied", traceId: "0af7651916cd43dd8448eb211c80319c" },
    }),
    {
      status: "failed",
      message: directoryAccessDenied,
      code: "directory.access-denied",
      reference: "0af7651916cd43dd8448eb211c80319c",
    },
  );
});

test("directoryFailure_DirectoryDownOrSlow_SaysSoWithoutACode", () => {
  assert.deepEqual(
    directoryFailure({
      status: 503,
      problem: { status: 503, code: "directory.unavailable", traceId: "b7ad6b7169203331" },
    }),
    { status: "failed", message: directoryUnavailable, reference: "b7ad6b7169203331" },
  );
  assert.deepEqual(directoryFailure({ status: 504, problem: { status: 504, code: "request.timeout" } }), {
    status: "failed",
    message: directoryTimedOut,
  });
});

test("directoryFailure_AnyOtherAnswer_IsLeftToTheFormsKit", () => {
  for (const [status, code] of [
    [403, "request.forbidden"],
    [503, undefined],
    [500, "directory.unavailable"],
    [404, "directory.person-not-found"],
    [409, "user.entra-object-id-taken"],
  ] as const) {
    assert.equal(
      directoryFailure({ status, problem: code === undefined ? { status } : { status, code } }),
      undefined,
      `${status} ${code ?? "without a code"}`,
    );
  }
});

import assert from "node:assert/strict";
import { test } from "node:test";

import { settleList, type ListSettling } from "./settle-list.ts";

class SignInAgain extends Error {}

const switchedOn: ListSettling = {
  switchedOn: () => Promise.resolve(true),
  rethrow: (error) => {
    if (error instanceof SignInAgain) throw error;
  },
};

test("settleList_OneListFailsWhileAnotherIsRead_AnswersEachOnItsOwn", async () => {
  const [countries, currencies] = await Promise.all([
    settleList(() => Promise.reject(new Error("The API answered 500.")), switchedOn),
    settleList(() => Promise.resolve([{ code: "USD", name: "US Dollar" }]), switchedOn),
  ]);

  assert.deepEqual(countries, { status: "failed" });
  assert.deepEqual(currencies, { status: "read", items: [{ code: "USD", name: "US Dollar" }] });
});

test("settleList_ModuleSwitchedOff_IsDisabledWithoutReadingTheList", async () => {
  let loads = 0;
  const read = await settleList(
    () => {
      loads += 1;
      return Promise.resolve([]);
    },
    { ...switchedOn, switchedOn: () => Promise.resolve(false) },
  );

  assert.deepEqual(read, { status: "disabled" });
  assert.equal(loads, 0);
});

test("settleList_ErrorTheRethrowPassesOn_Propagates", async () => {
  const signInAgain = new SignInAgain("The session ended.");

  await assert.rejects(
    settleList(() => Promise.reject(signInAgain), switchedOn),
    (error) => error === signInAgain,
  );
});

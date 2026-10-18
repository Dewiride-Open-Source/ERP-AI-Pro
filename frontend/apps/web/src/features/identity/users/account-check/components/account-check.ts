import type { DirectoryAccountCheckResponse, PersonDeactivationReason } from "@dewiride/erp-api-client";

import type { Problem } from "@/shared/api/problem-details";
import { formFailed, type FailedFormState } from "@/shared/forms/state/form-state";

import { ownRecordInactive } from "../../details/components/person-action-text.ts";
import { countLabel } from "../../organisation/components/disclosure-tree.ts";
import { organisationBusy } from "../../positions/forms/record-move.schema.ts";

export type CheckedPerson = {
  readonly id: string;
  readonly displayName: string;
  readonly reason: PersonDeactivationReason;
};

export type AccountCheckResult = {
  readonly status: "checked";
  readonly checkedCount: number;
  readonly deactivated: readonly CheckedPerson[];
};

export type AccountCheckRefusal = {
  readonly status: "refused";
  readonly message: string;
  readonly reference?: string | undefined;
};

export type AccountCheckAnswer = AccountCheckResult | AccountCheckRefusal;

export const accountCheckTitle = "Check Microsoft accounts";

export const accountCheckDescription =
  "The ERP asks Microsoft about everyone in People who is linked to a Microsoft account, and deactivates each person whose account is disabled or deleted. Any page they have open ends at their next click. You can reactivate a person from their page.";

export const checkingAccounts = "Asking Microsoft about the accounts…";

export const deactivatedListLabel = "People the check deactivated";

export const microsoftAccessDenied =
  "Microsoft has not granted the ERP permission to read accounts, so no one was checked. Whoever looks after your organisation's Microsoft Entra needs to grant it.";

export const microsoftUnavailable =
  "Microsoft could not be reached or gave no usable answer, so no one was checked. Try again in a few minutes.";

export const checkTimedOut = "The check took too long and was stopped. Try again in a few minutes.";

const reasonLabels: Readonly<Record<PersonDeactivationReason, string>> = {
  accountDisabled: "Microsoft account disabled",
  accountDeleted: "Microsoft account deleted",
  administrator: "Deactivated by an administrator",
};

export function accountCheckResult(response: DirectoryAccountCheckResponse): AccountCheckResult {
  return {
    status: "checked",
    checkedCount: response.checkedCount ?? 0,
    deactivated: (response.deactivated ?? []).flatMap(({ id, displayName, deactivatedBecause }) =>
      id && displayName && deactivatedBecause ? [{ id, displayName, reason: deactivatedBecause }] : [],
    ),
  };
}

export function accountCheckSummary({ checkedCount, deactivated }: AccountCheckResult): string {
  if (checkedCount === 0) {
    return "No active person in the ERP is linked to a Microsoft account, so there was no one to check.";
  }
  const checked = `Checked ${countLabel(checkedCount, "person", "people")}.`;
  if (deactivated.length === 0) return `${checked} Every account is active.`;
  const people = countLabel(deactivated.length, "person", "people");
  return `${checked} Deactivated ${people} whose Microsoft account is disabled or deleted.`;
}

export function deactivationReasonLabel(reason: PersonDeactivationReason): string {
  return reasonLabels[reason];
}

// The check reads the accounts with the ERP's own permission, which the organisation grants in Microsoft Entra, so a refusal
// names that grant rather than the administrator's sign-in. The forms kit words every 5xx as a fault of the ERP, while these
// answers mean Microsoft could not be asked, and a 409 means the administrator's own record or another organisation change
// stopped the check.
export function accountCheckFailure({
  status,
  problem,
}: {
  readonly status: number;
  readonly problem: Problem;
}): FailedFormState | undefined {
  const reference = { reference: problem.traceId };
  if (status === 403 && problem.code === "directory.access-denied") {
    return formFailed(microsoftAccessDenied, reference);
  }
  if (status === 503 && problem.code === "directory.unavailable")
    return formFailed(microsoftUnavailable, reference);
  if (status === 504 && problem.code === "request.timeout") return formFailed(checkTimedOut, reference);
  if (status === 409 && problem.code === "user.actor-inactive")
    return formFailed(ownRecordInactive, reference);
  if (status === 409 && problem.code === "concurrency.conflict")
    return formFailed(organisationBusy, reference);
  return undefined;
}

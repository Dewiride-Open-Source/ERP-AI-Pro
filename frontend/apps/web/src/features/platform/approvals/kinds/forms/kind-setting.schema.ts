import { z } from "zod";

import { formMessages } from "@/shared/forms/errors/form-messages";
import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";

import type { ApprovalKindItem } from "../components/kind-items.ts";

export const minWorkingDays = 1;

export const maxWorkingDays = 30;

const kindKeyMaxLength = 128;

const versionMaxLength = 64;

const workingDaysMessage = `Give each step ${minWorkingDays} to ${maxWorkingDays} working days.`;

const wholeNumber = /^\d+$/;

const workingDaysSchema = z
  .string({ error: workingDaysMessage })
  .trim()
  .refine(
    (text) => wholeNumber.test(text) && Number(text) >= minWorkingDays && Number(text) <= maxWorkingDays,
    { error: workingDaysMessage },
  )
  .transform(Number);

const requiredSchema = z.boolean({ error: formMessages.pageOutOfDate });

export const setUpKindSettingSchema = z.object({
  kind: z
    .string({ error: formMessages.pageOutOfDate })
    .min(1, { error: formMessages.pageOutOfDate })
    .max(kindKeyMaxLength, { error: formMessages.pageOutOfDate }),
  required: requiredSchema,
  workingDaysToDecide: workingDaysSchema,
});

export const changeKindSettingSchema = z.object({
  id: z.guid({ error: formMessages.pageOutOfDate }),
  version: z
    .string({ error: formMessages.pageOutOfDate })
    .min(1, { error: formMessages.pageOutOfDate })
    .max(versionMaxLength, { error: formMessages.pageOutOfDate }),
  required: requiredSchema,
  workingDaysToDecide: workingDaysSchema,
});

export type SetUpKindSettingValues = z.input<typeof setUpKindSettingSchema>;

export type ChangeKindSettingValues = z.input<typeof changeKindSettingSchema>;

// A kind nobody has set up starts from what it declares: it needs approval and gives each step its own working days.
export function setUpKindSettingValues(kind: ApprovalKindItem): SetUpKindSettingValues {
  return { kind: kind.key, required: true, workingDaysToDecide: String(kind.declaredWorkingDays) };
}

export function changeKindSettingValues(kind: ApprovalKindItem): ChangeKindSettingValues {
  const { setting } = kind;
  if (setting === null) throw new RangeError(`${kind.title} has no setting to change.`);
  return {
    id: setting.id,
    version: setting.version,
    required: setting.required,
    workingDaysToDecide: String(setting.workingDaysToDecide),
  };
}

export const kindSettingProblemMessages: ProblemMessages = {
  "approval.setting-exists":
    "Someone set this kind of request up after the page was loaded. Show the latest settings, then change it if needed.",
  "concurrency.conflict":
    "Someone changed this setting while you worked. Show the latest settings, then try again.",
  "approval.setting-not-found": "This setting no longer exists. Show the latest settings, then try again.",
};

export const kindSettingOutOfDateCodes: ReadonlySet<string> = new Set(
  Object.keys(kindSettingProblemMessages),
);

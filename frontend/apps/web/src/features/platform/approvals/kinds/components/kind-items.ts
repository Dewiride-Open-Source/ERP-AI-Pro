import type {
  ApprovalKindResponse,
  ApprovalKindSettingResponse,
  ApprovalKindStepResponse,
  ApprovalStepRule,
  ErpRole,
} from "@dewiride/erp-api-client";

export interface KindStepItem {
  readonly order: number;
  readonly title: string;
  readonly rule: ApprovalStepRule;
  readonly role: ErpRole | undefined;
}

export interface KindSettingItem {
  readonly id: string;
  readonly required: boolean;
  readonly workingDaysToDecide: number;
  readonly version: string;
}

export interface ApprovalKindItem {
  readonly key: string;
  readonly title: string;
  readonly description: string;
  readonly steps: readonly KindStepItem[];
  readonly declaredWorkingDays: number;
  readonly setting: KindSettingItem | null;
}

export class UnreadableAnswerError extends TypeError {
  override readonly name = "UnreadableAnswerError";
}

// The API's contract names every member below as present, so a member that arrives without a value means the answer cannot be
// shown faithfully, and the page reports the kinds as unreadable rather than show part of one.
function given<T>(value: T | null | undefined, member: string): T {
  if (value === null || value === undefined) {
    throw new UnreadableAnswerError(`The API answered a kind of request without its ${member}.`);
  }
  return value;
}

function named(value: string | null | undefined, member: string): string {
  const text = given(value, member);
  if (text.trim() === "") {
    throw new UnreadableAnswerError(`The API answered a kind of request with an empty ${member}.`);
  }
  return text;
}

// Kiota reads a JSON null where an object belongs as an object without members, so such an object is the API's null.
function present<T extends object>(value: T | null | undefined): T | null {
  return value === null || value === undefined || Object.keys(value).length === 0 ? null : value;
}

function stepOf(step: ApprovalKindStepResponse): KindStepItem {
  const rule = given(step.rule, "step's rule");
  const role = step.role ?? undefined;
  if (rule === "role" && role === undefined) {
    throw new UnreadableAnswerError("The API answered a role step without its role.");
  }
  return { order: given(step.order, "step's order"), title: named(step.title, "step's title"), rule, role };
}

function settingOf(setting: ApprovalKindSettingResponse | null | undefined): KindSettingItem | null {
  const answered = present(setting);
  return answered === null
    ? null
    : {
        id: named(answered.id, "setting's id"),
        required: given(answered.required, "setting's choice"),
        workingDaysToDecide: given(answered.workingDaysToDecide, "setting's working days"),
        version: named(answered.version, "setting's version"),
      };
}

export function approvalKindOf(kind: ApprovalKindResponse): ApprovalKindItem {
  return {
    key: named(kind.kind, "key"),
    title: named(kind.title, "title"),
    description: named(kind.description, "description"),
    steps: given(kind.steps, "steps").map(stepOf),
    declaredWorkingDays: given(kind.declaredWorkingDays, "working days"),
    setting: settingOf(kind.setting),
  };
}

// A kind no administrator has set up needs approval and gives each step the working days it declares.
export function requiresApproval(kind: ApprovalKindItem): boolean {
  return kind.setting?.required ?? true;
}

export function workingDaysInForce(kind: ApprovalKindItem): number {
  return kind.setting?.workingDaysToDecide ?? kind.declaredWorkingDays;
}

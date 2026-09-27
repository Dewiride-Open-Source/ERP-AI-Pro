import { z } from "zod";

import type { FieldAliases } from "@/shared/forms/errors/field-paths";
import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";
import {
  amountSchema,
  calendarDateSchema,
  dateRangeSchema,
  optionalInput,
  requiredText,
} from "@/shared/forms/schemas/field-schemas";
import { gstinSchema, ifscSchema, panSchema } from "@/shared/forms/schemas/indian-identifiers";

export const legalNameMaxLength = 200;

export const openingBalanceMaximum = "100000000";

export const agreementStartEarliest = "2020-04-01";

export const agreementStartLatest = "2035-03-31";

export const supplierStates = ["karnataka", "maharashtra", "rajasthan", "tamil-nadu"] as const;

export type SupplierState = (typeof supplierStates)[number];

export const supplierCategories = [
  "audit-and-tax",
  "cafe-and-pantry",
  "cloud-hosting",
  "computer-hardware",
  "contract-staff",
  "courier",
  "internet-and-telephone",
  "legal-services",
  "office-rent",
  "office-supplies",
  "recruitment",
  "software-licences",
  "training",
  "travel",
] as const;

export type SupplierCategory = (typeof supplierCategories)[number];

export const serverAnswers = [
  "accept",
  "field-error",
  "nested-problem",
  "conflict",
  "server-error",
  "unreachable",
] as const;

export type ServerAnswer = (typeof serverAnswers)[number];

export const supplierAlreadyRegisteredCode = "vendors.supplier-already-registered";

const stateMissing = "Choose the state of the registered office.";

const categoryMissing = "Choose a category from the list.";

const serverAnswerMissing = "Choose how the server answers.";

export const supplierExampleSchema = z.object({
  legalName: requiredText(legalNameMaxLength, { required: "Enter the supplier's legal name." }),
  gstin: gstinSchema,
  pan: panSchema,
  ifsc: ifscSchema,
  state: z.string({ error: stateMissing }).pipe(z.enum(supplierStates, { error: stateMissing })),
  category: z.string({ error: categoryMissing }).pipe(z.enum(supplierCategories, { error: categoryMissing })),
  openingBalance: amountSchema({ scale: 2, required: false, max: openingBalanceMaximum }),
  agreementStart: calendarDateSchema({
    required: true,
    min: agreementStartEarliest,
    max: agreementStartLatest,
    requiredMessage: "Enter the date the agreement starts.",
  }),
  validity: dateRangeSchema({ required: true }),
  agreementDocumentId: optionalInput(z.uuid({ error: "Upload the agreement document again." })),
  serverAnswer: z
    .string({ error: serverAnswerMissing })
    .pipe(z.enum(serverAnswers, { error: serverAnswerMissing })),
});

export type SupplierExampleValues = z.input<typeof supplierExampleSchema>;

export type SupplierExample = z.output<typeof supplierExampleSchema>;

export const defaultSupplierExampleValues: SupplierExampleValues = {
  legalName: "",
  gstin: "",
  pan: "",
  ifsc: "",
  state: "",
  category: "",
  openingBalance: "",
  agreementStart: "",
  validity: { from: "", to: "" },
  agreementDocumentId: "",
  serverAnswer: "accept",
};

export const supplierExampleFieldAliases: FieldAliases = {
  "registration.gstin": "gstin",
  "registration.pan": "pan",
  "bankAccount.ifsc": "ifsc",
  "balances[0].amount": "openingBalance",
  "agreement.validity": "validity",
  "agreement.startsOn": "agreementStart",
  "agreement.documentId": "agreementDocumentId",
};

export const supplierExampleProblemMessages: ProblemMessages = {
  [supplierAlreadyRegisteredCode]:
    "A supplier with this GSTIN is already registered. Open that supplier instead of adding it again.",
};

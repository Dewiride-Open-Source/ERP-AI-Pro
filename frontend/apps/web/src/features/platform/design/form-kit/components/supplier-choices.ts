import type { ComboboxOption } from "@dewiride/erp-ui/components/combobox/combobox";

import {
  supplierCategories,
  type ServerAnswer,
  type SupplierCategory,
  type SupplierState,
} from "../forms/supplier-example.schema";

export const stateLabels: Readonly<Record<SupplierState, string>> = {
  karnataka: "Karnataka",
  maharashtra: "Maharashtra",
  rajasthan: "Rajasthan",
  "tamil-nadu": "Tamil Nadu",
};

const categoryLabels: Readonly<Record<SupplierCategory, Omit<ComboboxOption, "value">>> = {
  "audit-and-tax": { label: "Audit and tax", description: "Statutory audit, tax audit and returns" },
  "cafe-and-pantry": { label: "Café and pantry" },
  "cloud-hosting": { label: "Cloud hosting" },
  "computer-hardware": { label: "Computer hardware" },
  "contract-staff": { label: "Contract staff", description: "People engaged through an agency" },
  courier: { label: "Courier" },
  "internet-and-telephone": { label: "Internet and telephone" },
  "legal-services": { label: "Legal services" },
  "office-rent": { label: "Office rent" },
  "office-supplies": { label: "Office supplies" },
  recruitment: { label: "Recruitment" },
  "software-licences": { label: "Software licences", description: "Subscriptions and perpetual licences" },
  training: { label: "Training" },
  travel: { label: "Travel" },
};

const unavailableCategory: ComboboxOption = {
  value: "imports",
  label: "Imports",
  description: "Not enabled for this company",
  disabled: true,
};

export const categoryOptions: readonly ComboboxOption[] = [
  ...supplierCategories.map((value) => ({ value, ...categoryLabels[value] })),
  unavailableCategory,
].sort((left, right) => left.label.localeCompare(right.label, "en-IN"));

export const serverAnswerLabels: Readonly<Record<ServerAnswer, string>> = {
  accept: "Accept the supplier",
  "field-error": "Refuse the legal name",
  "nested-problem": "Refuse several nested details",
  conflict: "Report the supplier as already registered",
  "server-error": "Fail with a server error",
  unreachable: "Do not respond (API unreachable)",
};

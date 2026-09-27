"use client";

import { Combobox, type ComboboxOption } from "@dewiride/erp-ui/components/combobox/combobox";
import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { useState } from "react";

import { Specimen } from "../specimen";

const expenseAccounts: readonly ComboboxOption[] = [
  { value: "advertising", label: "Advertising" },
  { value: "audit-fees", label: "Audit fees", description: "Statutory and tax audit" },
  { value: "bank-charges", label: "Bank charges" },
  { value: "books", label: "Books and periodicals" },
  { value: "cafe", label: "Café and pantry" },
  { value: "communication", label: "Communication", description: "Internet and telephone" },
  { value: "conveyance", label: "Conveyance" },
  { value: "depreciation", label: "Depreciation", description: "Posted by the system", disabled: true },
  { value: "electricity", label: "Electricity" },
  { value: "insurance", label: "Insurance" },
  { value: "office-rent", label: "Office rent" },
  { value: "printing", label: "Printing and stationery" },
  { value: "professional-fees", label: "Professional fees" },
  { value: "repairs", label: "Repairs and maintenance" },
  { value: "software", label: "Software subscriptions" },
  { value: "staff-welfare", label: "Staff welfare" },
  { value: "travel", label: "Travel" },
];

const costCentres: readonly ComboboxOption[] = [
  { value: "delivery", label: "Delivery" },
  { value: "sales", label: "Sales" },
  { value: "administration", label: "Administration" },
];

const branches: readonly ComboboxOption[] = [
  { value: "bengaluru", label: "Bengaluru" },
  { value: "pune", label: "Pune" },
];

export function ComboboxSpecimen() {
  const [account, setAccount] = useState<string | null>(null);
  const [defaultAccount, setDefaultAccount] = useState<string | null>("professional-fees");
  const [costCentre, setCostCentre] = useState<string | null>(null);
  const [clientSearch, setClientSearch] = useState("Acme");

  return (
    <Specimen
      title="Combobox"
      description="Type to narrow the list, move with the arrow keys and choose with Enter; only a listed option can be chosen."
    >
      <FieldGroup>
        <FormField
          id="ks-forms-expense-account"
          label="Expense account"
          description="Accents are ignored: cafe finds Café."
        >
          {(frame) => (
            <Combobox
              {...frame}
              options={expenseAccounts}
              value={account}
              onValueChange={setAccount}
              placeholder="Search accounts"
              emptyMessage="No account matches."
            />
          )}
        </FormField>
        <p className="text-caption text-muted-foreground" data-testid="forms-combobox-value">
          Value: <span className="font-mono">{account ?? "(none)"}</span>
        </p>
        <FormField id="ks-forms-default-account" label="Default expense account" required>
          {(frame) => (
            <Combobox
              {...frame}
              options={expenseAccounts}
              value={defaultAccount}
              onValueChange={setDefaultAccount}
              emptyMessage="No account matches."
            />
          )}
        </FormField>
        <FormField
          id="ks-forms-cost-centre"
          label="Cost centre"
          errors={costCentre === null ? ["Choose a cost centre from the list."] : undefined}
        >
          {(frame) => (
            <Combobox
              {...frame}
              options={costCentres}
              value={costCentre}
              onValueChange={setCostCentre}
              placeholder="Search cost centres"
              emptyMessage="No cost centre matches."
            />
          )}
        </FormField>
        <FormField id="ks-forms-branch" label="Branch" description="Fixed for this user." disabled>
          {(frame) => (
            <Combobox
              {...frame}
              options={branches}
              value="bengaluru"
              onValueChange={() => undefined}
              emptyMessage="No branch matches."
              disabled
            />
          )}
        </FormField>
        <FormField
          id="ks-forms-client-search"
          label="Client"
          description="Searched on the server: this one shows the state while an answer is awaited."
        >
          {(frame) => (
            <Combobox
              {...frame}
              options={[]}
              value={null}
              onValueChange={() => undefined}
              inputValue={clientSearch}
              onInputValueChange={setClientSearch}
              loading
              emptyMessage="No client matches."
            />
          )}
        </FormField>
      </FieldGroup>
    </Specimen>
  );
}

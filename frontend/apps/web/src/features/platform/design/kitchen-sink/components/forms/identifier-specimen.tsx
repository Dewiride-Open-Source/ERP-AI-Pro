"use client";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { IdentifierInput } from "@dewiride/erp-ui/components/inputs/identifier-input";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { useState } from "react";
import type { z } from "zod";

import {
  gstinSchema,
  identifierLengths,
  ifscSchema,
  panSchema,
} from "@/shared/forms/schemas/indian-identifiers";

import { Specimen } from "../specimen";

function problemsOf(schema: z.ZodType, value: string): readonly string[] | undefined {
  if (value === "") return undefined;
  const result = schema.safeParse(value);
  return result.success ? undefined : result.error.issues.map((issue) => issue.message);
}

export function IdentifierSpecimen() {
  const [gstin, setGstin] = useState("");
  const [pan, setPan] = useState("AAACD1234E");
  const [ifsc, setIfsc] = useState("HDFC000123");

  return (
    <Specimen
      title="GSTIN, PAN and IFSC"
      description="Upper-cased as you type or paste, with spaces and punctuation removed and the length capped; the shape is checked by the form."
    >
      <FieldGroup>
        <FormField
          id="ks-forms-gstin"
          label="GSTIN"
          description="Paste it with spaces or in lower case."
          errors={problemsOf(gstinSchema, gstin)}
        >
          {(frame) => (
            <IdentifierInput
              {...frame}
              value={gstin}
              onValueChange={setGstin}
              maxLength={identifierLengths.gstin}
            />
          )}
        </FormField>
        <p className="text-caption text-muted-foreground" data-testid="forms-gstin-value">
          Value: <span className="font-mono">{gstin === "" ? "(empty)" : gstin}</span>
        </p>
        <FormField id="ks-forms-pan" label="PAN" required errors={problemsOf(panSchema, pan)}>
          {(frame) => (
            <IdentifierInput
              {...frame}
              value={pan}
              onValueChange={setPan}
              maxLength={identifierLengths.pan}
            />
          )}
        </FormField>
        <FormField id="ks-forms-ifsc" label="IFSC" errors={problemsOf(ifscSchema, ifsc)}>
          {(frame) => (
            <IdentifierInput
              {...frame}
              value={ifsc}
              onValueChange={setIfsc}
              maxLength={identifierLengths.ifsc}
            />
          )}
        </FormField>
        <FormField
          id="ks-forms-branch-gstin"
          label="Branch GSTIN"
          description="Taken from the branch record."
          disabled
        >
          {(frame) => (
            <IdentifierInput
              {...frame}
              value="29AAACD1234E1Z3"
              onValueChange={() => undefined}
              maxLength={identifierLengths.gstin}
              disabled
            />
          )}
        </FormField>
      </FieldGroup>
    </Specimen>
  );
}

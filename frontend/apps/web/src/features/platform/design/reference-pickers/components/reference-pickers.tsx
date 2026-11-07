import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";

import { ReferenceListsProvider } from "@/features/platform/reference-data";

import { ReferencePickersForm } from "./reference-pickers-form";

const formHeadingId = "reference-pickers-heading";

export function ReferencePickers() {
  return (
    <div className="grid min-w-0 grid-cols-1 gap-section">
      <header className="grid gap-2">
        <p className="text-eyebrow text-primary uppercase">Platform</p>
        <h1 className="text-title">Reference pickers</h1>
        <p className="max-w-prose text-body text-muted-foreground">
          The fields other pages use to choose a country, a State code, a currency, a financial year and one
          of its periods, each offering what the reference lists hold. Nothing you choose here is saved.
        </p>
      </header>

      <Card>
        <CardHeader>
          <CardTitle>
            <h2 id={formHeadingId}>Choose from the reference lists (example)</h2>
          </CardTitle>
          <CardDescription>
            Type part of a name or a code to narrow a list, then choose with the arrow keys and Enter.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ReferenceListsProvider
            needs={{
              countries: true,
              indianStates: true,
              currencies: true,
              financialYears: { from: "2024-25", to: "2027-28" },
            }}
          >
            <ReferencePickersForm labelledBy={formHeadingId} />
          </ReferenceListsProvider>
        </CardContent>
      </Card>
    </div>
  );
}

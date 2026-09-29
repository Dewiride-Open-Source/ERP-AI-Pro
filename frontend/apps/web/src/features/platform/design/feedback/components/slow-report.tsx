import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import Link from "next/link";

import { formatRupees } from "@/shared/format/money";

const preparationMilliseconds = 3000;

const ageingBuckets = [
  { label: "Not yet due", amount: 1_845_200 },
  { label: "1 to 30 days overdue", amount: 612_450 },
  { label: "31 to 60 days overdue", amount: 238_900 },
  { label: "61 to 90 days overdue", amount: 97_300 },
  { label: "More than 90 days overdue", amount: 41_750 },
] as const;

// The report stands in for one whose data takes a while to gather, so the skeleton of its own loading.tsx is what a person
// sees while it is prepared.
export async function SlowReport() {
  await new Promise((resolve) => setTimeout(resolve, preparationMilliseconds));

  return (
    <div className="grid min-w-0 grid-cols-1 gap-section">
      <header className="grid gap-2">
        <p className="text-eyebrow text-primary uppercase">Platform</p>
        <h1 className="text-title">Receivables ageing (example)</h1>
        <p className="max-w-prose text-body text-muted-foreground">
          This example report takes three seconds to prepare, so the loading status shows while it is on its
          way.
        </p>
      </header>
      <Card>
        <CardHeader>
          <CardTitle>
            <h2>Amounts owed by clients</h2>
          </CardTitle>
          <CardDescription>Grouped by how long each invoice has been overdue.</CardDescription>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-2">
            {ageingBuckets.map((bucket) => (
              <div key={bucket.label} className="flex justify-between gap-4 border-b pb-2 last:border-b-0">
                <dt className="text-body text-muted-foreground">{bucket.label}</dt>
                <dd className="text-body font-medium tabular-nums">{formatRupees(bucket.amount)}</dd>
              </div>
            ))}
          </dl>
        </CardContent>
      </Card>
      <p className="text-body">
        <Link
          href="/design/feedback"
          className="rounded-sm font-medium text-primary underline-offset-4 focus-ring hover:underline"
        >
          Back to feedback
        </Link>
      </p>
    </div>
  );
}

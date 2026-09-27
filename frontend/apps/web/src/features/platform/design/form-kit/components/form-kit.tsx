import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";

import { SupplierExampleForm } from "./supplier-example-form";

const formHeadingId = "supplier-example-heading";

export function FormKit() {
  return (
    <div className="grid min-w-0 animate-fade-up grid-cols-1 gap-section">
      <header className="grid gap-2">
        <p className="text-eyebrow text-primary uppercase">Platform</p>
        <h1 className="text-title">Form kit</h1>
        <p className="max-w-prose text-body text-muted-foreground">
          One form built the way every ERP form is built: checked as you type, checked again by the server,
          with each answer the server can give shown where it belongs. Nothing you enter here is saved.
        </p>
      </header>

      <Card>
        <CardHeader>
          <CardTitle>
            <h2 id={formHeadingId}>Register a supplier (example)</h2>
          </CardTitle>
          <CardDescription>
            Choose how the server answers at the end of the form, then save to see where that answer lands.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <SupplierExampleForm labelledBy={formHeadingId} />
        </CardContent>
      </Card>
    </div>
  );
}

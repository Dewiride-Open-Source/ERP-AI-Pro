import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";

import { exchangeRatesNavigation } from "../../nav";
import { ReferenceListsProvider } from "../../reference-lists/components/reference-lists-provider";

import { todayInIndia } from "./exchange-rate-display";
import { NewExchangeRateForm } from "./new-exchange-rate-form";

export const newExchangeRateTitle = "Record a rate";

const formHeadingId = "new-exchange-rate-heading";

export function NewExchangeRate() {
  return (
    <div className="grid min-w-0 grid-cols-1 gap-section">
      <header className="grid gap-2">
        <p className="text-eyebrow text-primary uppercase">{exchangeRatesNavigation.title}</p>
        <h1 className="text-title">{newExchangeRateTitle}</h1>
      </header>

      <Card>
        <CardHeader>
          <CardTitle>
            <h2 id={formHeadingId}>The rate</h2>
          </CardTitle>
          <CardDescription>
            The rupees a foreign currency was worth on one day, as its source published them. A currency has
            one rate a day.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ReferenceListsProvider needs={{ currencies: true }}>
            <NewExchangeRateForm today={todayInIndia()} labelledBy={formHeadingId} />
          </ReferenceListsProvider>
        </CardContent>
      </Card>
    </div>
  );
}

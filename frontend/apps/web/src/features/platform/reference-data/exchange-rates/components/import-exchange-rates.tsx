import { ImportExchangeRatesForm } from "../../exchange-rate-imports/components/import-exchange-rates-form";
import { exchangeRatesNavigation } from "../../nav";
import { ReferenceListsProvider } from "../../reference-lists/components/reference-lists-provider";

import { todayInIndia } from "./exchange-rate-display";

export const importExchangeRatesTitle = "Import rates";

export function ImportExchangeRates() {
  return (
    <div className="grid min-w-0 grid-cols-1 gap-section">
      <header className="grid gap-2">
        <p className="text-eyebrow text-primary uppercase">{exchangeRatesNavigation.title}</p>
        <h1 className="text-title">{importExchangeRatesTitle}</h1>
        <p className="max-w-prose text-muted-foreground">
          Record many rates at once from a CSV file, such as one saved from Excel. The file is checked here
          before anything is sent, and its rates are recorded all together or not at all.
        </p>
      </header>

      <ReferenceListsProvider needs={{ currencies: true }}>
        <ImportExchangeRatesForm today={todayInIndia()} />
      </ReferenceListsProvider>
    </div>
  );
}

import { GlobeIcon, IndianRupeeIcon } from "lucide-react";

import { platformArea } from "../_shared/platform-area";

export const referenceListsNavigation = {
  id: "platform-reference-data-lists",
  title: "Reference lists",
  description:
    "See the countries, Indian states and union territories with their GST codes, and the currencies the ERP knows.",
  basePath: "/platform/reference-data",
  featureFlag: "Erp.Modules.Platform.ReferenceData",
  permission: "platform.reference-data.lists.read",
  area: platformArea,
  icon: GlobeIcon,
} as const;

export const exchangeRatesNavigation = {
  id: "platform-reference-data-exchange-rates",
  title: "Exchange rates",
  description: "Record the rupee value of foreign currencies by date, by hand or from a file.",
  basePath: "/platform/reference-data/exchange-rates",
  featureFlag: "Erp.Modules.Platform.ReferenceData",
  permission: "platform.reference-data.exchange-rates.read",
  area: platformArea,
  icon: IndianRupeeIcon,
} as const;

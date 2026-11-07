import { LandmarkIcon } from "lucide-react";

import { complianceArea } from "../_shared/compliance-area";

export const parameterSetsNavigation = {
  id: "compliance-statutory-parameters-parameter-sets",
  title: "Statutory parameters",
  description:
    "See the government rates, codes and due dates the ERP uses, with the date each applies from and what changed.",
  basePath: "/compliance/statutory-parameters/parameter-sets",
  featureFlag: "Erp.Modules.Compliance.StatutoryParameters",
  permission: "compliance.statutory-parameters.parameter-sets.read",
  area: complianceArea,
  icon: LandmarkIcon,
} as const;

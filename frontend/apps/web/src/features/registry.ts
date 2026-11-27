import { parameterSetsNavigation } from "@/features/compliance/statutory-parameters";
import {
  departmentsNavigation,
  designationsNavigation,
  orgChartNavigation,
  peopleNavigation,
} from "@/features/identity/users";
import { approvalsNavigation } from "@/features/platform/approvals";
import { attachmentsNavigation } from "@/features/platform/attachments";
import { auditNavigation } from "@/features/platform/audit";
import { jobsNavigation } from "@/features/platform/jobs";
import { numberingNavigation } from "@/features/platform/numbering";
import { exchangeRatesNavigation, referenceListsNavigation } from "@/features/platform/reference-data";
import { systemInfoNavigation } from "@/features/platform/system-info";
import type { NavigationEntry } from "@/shared/layout/navigation-entry";

export const navigation: readonly NavigationEntry[] = [
  systemInfoNavigation,
  attachmentsNavigation,
  auditNavigation,
  jobsNavigation,
  referenceListsNavigation,
  exchangeRatesNavigation,
  numberingNavigation,
  approvalsNavigation,
  peopleNavigation,
  orgChartNavigation,
  departmentsNavigation,
  designationsNavigation,
  parameterSetsNavigation,
];

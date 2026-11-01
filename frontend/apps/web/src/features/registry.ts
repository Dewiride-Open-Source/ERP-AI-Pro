import {
  departmentsNavigation,
  designationsNavigation,
  orgChartNavigation,
  peopleNavigation,
} from "@/features/identity/users";
import { attachmentsNavigation } from "@/features/platform/attachments";
import { auditNavigation } from "@/features/platform/audit";
import { jobsNavigation } from "@/features/platform/jobs";
import { systemInfoNavigation } from "@/features/platform/system-info";
import type { NavigationEntry } from "@/shared/layout/navigation-entry";

export const navigation: readonly NavigationEntry[] = [
  systemInfoNavigation,
  attachmentsNavigation,
  auditNavigation,
  jobsNavigation,
  peopleNavigation,
  orgChartNavigation,
  departmentsNavigation,
  designationsNavigation,
];

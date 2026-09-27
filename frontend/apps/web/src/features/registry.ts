import { attachmentsNavigation } from "@/features/platform/attachments";
import { systemInfoNavigation } from "@/features/platform/system-info";
import type { NavigationEntry } from "@/shared/layout/navigation-entry";

export const navigation: readonly NavigationEntry[] = [systemInfoNavigation, attachmentsNavigation];

import type { ReactNode } from "react";

import { navigation } from "@/features/registry";
import { AppShell } from "@/shared/layout/app-shell";

export default function AppLayout({ children }: Readonly<{ children: ReactNode }>) {
  return <AppShell navigation={navigation}>{children}</AppShell>;
}

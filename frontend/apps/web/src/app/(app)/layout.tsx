import type { ReactNode } from "react";

import { AccountArea } from "@/features/identity/auth";
import { navigation } from "@/features/registry";
import { AppShell } from "@/shared/layout/app-shell";

export default function AppLayout({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <AppShell navigation={navigation} account={<AccountArea />}>
      {children}
    </AppShell>
  );
}

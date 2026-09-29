import type { ReactNode } from "react";

import { AppPage } from "@/shared/layout/app-page";

export default function AppTemplate({ children }: Readonly<{ children: ReactNode }>) {
  return <AppPage>{children}</AppPage>;
}

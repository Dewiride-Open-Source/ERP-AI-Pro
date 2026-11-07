import type { ReactNode } from "react";

import { ReferenceListsTabs } from "@/features/platform/reference-data";

export default function ReferenceListsLayout({ children }: Readonly<{ children: ReactNode }>) {
  return <ReferenceListsTabs>{children}</ReferenceListsTabs>;
}

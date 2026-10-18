import type { ReactNode } from "react";

import { peopleNavigation } from "@/features/identity/users";
import { requireFeature } from "@/shared/feature-flags/require-feature";

export default async function ProfileLayout({ children }: Readonly<{ children: ReactNode }>) {
  await requireFeature(peopleNavigation.featureFlag);
  return children;
}

import type { Metadata } from "next";

import { PeopleOverview, peopleNavigation } from "@/features/identity/users";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata("People", peopleNavigation.permission);
}

export default async function PeopleListPage({ searchParams }: Readonly<PageProps<"/identity/users">>) {
  return (
    <RequirePermission permission={peopleNavigation.permission}>
      <PeopleOverview searchParameters={await searchParams} />
    </RequirePermission>
  );
}

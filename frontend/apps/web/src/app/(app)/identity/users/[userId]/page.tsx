import type { Metadata } from "next";

import { PersonOverview, peopleNavigation, personPageTitle } from "@/features/identity/users";
import { RequirePermission } from "@/shared/auth/require-permission";

export async function generateMetadata({ params }: PageProps<"/identity/users/[userId]">): Promise<Metadata> {
  return personPageTitle((await params).userId);
}

export default async function PersonPage({ params }: Readonly<PageProps<"/identity/users/[userId]">>) {
  const { userId } = await params;
  return (
    <RequirePermission permission={peopleNavigation.permission}>
      <PersonOverview userId={userId} />
    </RequirePermission>
  );
}

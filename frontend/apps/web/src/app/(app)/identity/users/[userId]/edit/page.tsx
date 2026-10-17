import type { Metadata } from "next";

import { EditPerson, editPersonPageTitle } from "@/features/identity/users";
import { RequirePermission } from "@/shared/auth/require-permission";

export async function generateMetadata({
  params,
}: PageProps<"/identity/users/[userId]/edit">): Promise<Metadata> {
  return editPersonPageTitle((await params).userId);
}

export default async function EditPersonPage({
  params,
}: Readonly<PageProps<"/identity/users/[userId]/edit">>) {
  const { userId } = await params;
  return (
    <RequirePermission permission="identity.users.people.update">
      <EditPerson userId={userId} />
    </RequirePermission>
  );
}

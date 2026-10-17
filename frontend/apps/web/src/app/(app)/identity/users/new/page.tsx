import type { Metadata } from "next";

import { RegisterPerson } from "@/features/identity/users";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata("Register a person", "identity.users.people.register");
}

export default function RegisterPersonPage() {
  return (
    <RequirePermission permission="identity.users.people.register">
      <RegisterPerson />
    </RequirePermission>
  );
}

import { UsersIcon } from "lucide-react";

import { identityArea } from "../_shared/identity-area";

export const peopleNavigation = {
  id: "identity-users",
  title: "People",
  description: "Register people, assign their roles and record their moves.",
  basePath: "/identity/users",
  featureFlag: "Erp.Modules.Identity.Users",
  permission: "identity.users.people.read",
  area: identityArea,
  icon: UsersIcon,
} as const;

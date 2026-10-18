import { Building2Icon, IdCardIcon, NetworkIcon, UsersIcon } from "lucide-react";

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

export const orgChartNavigation = {
  id: "identity-users-org-chart",
  title: "Organisation chart",
  description: "See who works in each department and who reports to whom.",
  basePath: "/identity/users/org-chart",
  featureFlag: "Erp.Modules.Identity.Users",
  permission: "identity.users.org-chart.read",
  area: identityArea,
  icon: NetworkIcon,
} as const;

export const departmentsNavigation = {
  id: "identity-users-departments",
  title: "Departments",
  description: "See the company's departments and which department each one sits under.",
  basePath: "/identity/users/departments",
  featureFlag: "Erp.Modules.Identity.Users",
  permission: "identity.users.departments.read",
  area: identityArea,
  icon: Building2Icon,
} as const;

export const designationsNavigation = {
  id: "identity-users-designations",
  title: "Designations",
  description: "See the designations people can hold in the company.",
  basePath: "/identity/users/designations",
  featureFlag: "Erp.Modules.Identity.Users",
  permission: "identity.users.designations.read",
  area: identityArea,
  icon: IdCardIcon,
} as const;

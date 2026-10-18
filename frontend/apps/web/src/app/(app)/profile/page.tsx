import type { Metadata } from "next";

import { ProfileOverview, profileTitle } from "@/features/identity/users";

export function generateMetadata(): Metadata {
  return { title: profileTitle };
}

export default function ProfilePage() {
  return <ProfileOverview />;
}

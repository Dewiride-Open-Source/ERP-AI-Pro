import type { Metadata } from "next";

import { LoginCard } from "@/features/identity/auth";

export const metadata: Metadata = { title: "Sign in" };

export default function LoginPage() {
  return <LoginCard />;
}

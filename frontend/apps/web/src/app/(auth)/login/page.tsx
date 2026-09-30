import type { Metadata } from "next";

import { LoginCard } from "@/features/identity/auth";

export const metadata: Metadata = { title: "Sign in" };

export default async function LoginPage({ searchParams }: Readonly<PageProps<"/login">>) {
  return <LoginCard searchParameters={await searchParams} />;
}

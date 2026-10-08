import type { Metadata } from "next";

import { LoginCard, loginPageTitle } from "@/features/identity/auth";

export async function generateMetadata({ searchParams }: Readonly<PageProps<"/login">>): Promise<Metadata> {
  return { title: loginPageTitle(await searchParams) };
}

export default async function LoginPage({ searchParams }: Readonly<PageProps<"/login">>) {
  return <LoginCard searchParameters={await searchParams} />;
}

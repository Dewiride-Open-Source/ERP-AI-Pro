import type { Metadata } from "next";

import { StartPage } from "@/features/platform/home";
import { navigation } from "@/features/registry";

export const metadata: Metadata = { title: "Home" };

export default function HomePage() {
  return <StartPage navigation={navigation} />;
}

import type { Metadata } from "next";

import { FormKit } from "@/features/platform/design";

export const metadata: Metadata = { title: "Form kit" };

export default function FormKitPage() {
  return <FormKit />;
}

"use client";

import { SubmitButton } from "@dewiride/erp-ui/components/forms/submit-button";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { LogOutIcon } from "lucide-react";

import { useSignOutForm } from "../hooks/use-sign-out-form";

export function SignOutButton() {
  const hydrated = useHydrated();
  const { pending, formProps, tokenFieldProps } = useSignOutForm();

  return (
    <form {...formProps} className="grid">
      <input {...tokenFieldProps} />
      <SubmitButton pending={pending} ready={hydrated} variant="outline">
        {pending ? null : <LogOutIcon aria-hidden />}
        Sign out
      </SubmitButton>
    </form>
  );
}

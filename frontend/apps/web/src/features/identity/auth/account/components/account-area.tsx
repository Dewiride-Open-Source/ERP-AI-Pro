import { requireSession } from "@/shared/auth/session";

import { SessionKeeper } from "../../session/components/session-keeper";

import { SignOutButton } from "./sign-out-button";

// The shell renders this on every full page load, so the session check runs before anything of the page is shown; an API
// that cannot be reached shows the page without the person, as every other read does.
export async function AccountArea() {
  const session = await requireSession();
  if (session.status !== "signed-in") return null;

  return (
    <div className="flex items-center gap-1" data-testid="account-area">
      <p className="sr-only max-w-48 truncate text-sm text-muted-foreground md:not-sr-only">
        <span className="sr-only">Signed in as </span>
        {session.person.name}
      </p>
      <SignOutButton compact />
      <SessionKeeper />
    </div>
  );
}

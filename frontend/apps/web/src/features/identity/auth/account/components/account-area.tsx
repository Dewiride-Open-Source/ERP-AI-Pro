import { requireSession } from "@/shared/auth/session";

import { SessionKeeper } from "../../session/components/session-keeper";

import { SignOutButton } from "./sign-out-button";

// The shell renders this on every full page load, so the session check runs before anything of the page is shown. Sign out
// and the session keeper stay on a page whose API could not answer, because the browser still holds the session and its
// Microsoft sign-in, and a refresh of that page keeps the same keeper; only the person's name waits for the API.
export async function AccountArea() {
  const session = await requireSession();

  return (
    <div className="flex items-center gap-1" data-testid="account-area">
      {session.status === "signed-in" ? (
        <p
          className="max-w-48 truncate text-sm text-muted-foreground max-md:sr-only"
          title={session.person.name}
        >
          <span className="sr-only">Signed in as </span>
          {session.person.name}
        </p>
      ) : null}
      <SignOutButton compact />
      <SessionKeeper />
    </div>
  );
}

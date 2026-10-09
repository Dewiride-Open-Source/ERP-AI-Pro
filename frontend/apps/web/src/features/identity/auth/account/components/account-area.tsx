import { requireSession } from "@/shared/auth/session";

import { SessionKeeper } from "../../session/components/session-keeper";

import { UserMenu } from "./user-menu";

// The shell renders this on every full page load, so the session check runs before anything of the page is shown. The
// user menu, with its sign-out, and the session keeper stay on a page whose API could not answer, because the browser
// still holds the session and its Microsoft sign-in, and a refresh of that page keeps the same keeper; only the person's
// name waits for the API.
export async function AccountArea() {
  const session = await requireSession();

  return (
    <div className="flex items-center" data-testid="account-area">
      <UserMenu
        person={
          session.status === "signed-in"
            ? { name: session.person.name, userName: session.person.userName }
            : undefined
        }
      />
      <SessionKeeper />
    </div>
  );
}

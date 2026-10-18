import { Suspense } from "react";

import { OwnThemeKeeper, ownPhotoPath, peopleNavigation } from "@/features/identity/users";
import { requireSession } from "@/shared/auth/session";
import { isFeatureEnabled } from "@/shared/feature-flags/feature-flags";
import { getFeatureFlags } from "@/shared/feature-flags/queries";
import { personInitials } from "@/shared/format/person-initials";

import { SessionKeeper } from "../../session/components/session-keeper";

import { UserMenu } from "./user-menu";

// The shell renders this on every full page load, so the session check runs before anything of the page is shown. The
// user menu, with its sign-out, and the session keeper stay on a page whose API could not answer, because the browser
// still holds the session and its Microsoft sign-in, and a refresh of that page keeps the same keeper; only the person's
// name waits for the API. The person's own record, with their photo, My profile and the theme they saved, belongs to the
// users module, so none of it is offered while that module is off; the theme keeper streams in after the page, so its read
// never holds the page back.
export async function AccountArea() {
  const [session, flags] = await Promise.all([requireSession(), getFeatureFlags()]);
  const ownRecord = isFeatureEnabled(flags, peopleNavigation.featureFlag);
  const signedIn = session.status === "signed-in";

  return (
    <div className="flex items-center" data-testid="account-area">
      <UserMenu
        person={
          signedIn
            ? {
                name: session.person.name,
                userName: session.person.userName,
                initials: personInitials(session.person.name),
                photo: ownRecord ? ownPhotoPath : undefined,
              }
            : undefined
        }
        showsProfile={ownRecord}
      />
      <SessionKeeper />
      {signedIn && ownRecord ? (
        <Suspense fallback={null}>
          <OwnThemeKeeper />
        </Suspense>
      ) : null}
    </div>
  );
}

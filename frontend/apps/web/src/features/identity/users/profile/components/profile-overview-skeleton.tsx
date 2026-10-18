import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

import { profileTitle } from "./profile-text";

const detailPlaceholders = ["code", "phone", "joined", "department", "designation", "manager"];

export function ProfileOverviewSkeleton() {
  return (
    <LoadingStatus
      label="Loading your profile"
      pageTitle={profileTitle}
      className="grid gap-section"
      data-testid="profile-loading"
    >
      <header className="grid w-full max-w-prose gap-2">
        <Skeleton className="h-9 w-full max-w-56" />
        <Skeleton className="h-4 w-full" />
      </header>

      <Card>
        <CardContent className="flex items-center gap-4">
          <Skeleton className="size-24 shrink-0 rounded-full" />
          <div className="grid w-full max-w-72 gap-2">
            <Skeleton className="h-6 w-full" />
            <Skeleton className="h-4 w-full max-w-56" />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-full max-w-40" />
          <Skeleton className="h-4 w-full max-w-96" />
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          {detailPlaceholders.map((placeholder) => (
            <Skeleton key={placeholder} className="h-18 w-full" />
          ))}
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { ArrowRightIcon } from "lucide-react";
import Link from "next/link";

import { requireSession } from "@/shared/auth/session";
import { isFeatureEnabled } from "@/shared/feature-flags/feature-flags";
import { getFeatureFlags } from "@/shared/feature-flags/queries";
import type { NavigationEntry } from "@/shared/layout/navigation-entry";

const areasHeadingId = "start-areas-heading";

export async function StartPage({ navigation }: { navigation: readonly NavigationEntry[] }) {
  const [session, flags] = await Promise.all([requireSession(), getFeatureFlags()]);
  const areas = navigation.filter((entry) => isFeatureEnabled(flags, entry.featureFlag));

  return (
    <div className="grid gap-section">
      <header className="grid gap-2">
        <p className="text-eyebrow text-primary uppercase">Home</p>
        <h1 className="text-title">
          {session.status === "signed-in" ? `Welcome, ${session.person.name}` : "Welcome"}
        </h1>
        <p className="text-muted-foreground">Choose where to start.</p>
      </header>

      <section aria-labelledby={areasHeadingId} className="grid gap-4">
        <h2 id={areasHeadingId} className="sr-only">
          Areas you can open
        </h2>
        {areas.length === 0 ? (
          <p
            className="rounded-lg border border-dashed p-6 text-muted-foreground"
            data-testid="start-no-areas"
          >
            Nothing is open to you yet. Ask your administrator for access.
          </p>
        ) : (
          <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {areas.map((area) => (
              <li key={area.id}>
                <Card className="h-full" data-testid="start-area">
                  <CardHeader>
                    <CardTitle>
                      <h3>{area.title}</h3>
                    </CardTitle>
                    <CardDescription>{area.description}</CardDescription>
                  </CardHeader>
                  <CardContent className="mt-auto">
                    <Link
                      href={area.basePath}
                      className="inline-flex items-center gap-1.5 rounded-md text-sm font-medium text-primary focus-ring hover:underline"
                    >
                      Open {area.title}
                      <ArrowRightIcon className="size-4" aria-hidden />
                    </Link>
                  </CardContent>
                </Card>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

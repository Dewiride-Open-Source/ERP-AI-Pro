import { ErrorState } from "@dewiride/erp-ui/components/feedback/error-state";

export function ErrorPage({
  digest,
  onRetry,
  withinShell = false,
}: {
  digest: string | undefined;
  onRetry: () => void;
  withinShell?: boolean;
}) {
  const state = (
    <ErrorState
      headingLevel={1}
      focusOnMount
      title="We could not show this page"
      description="Something went wrong while preparing it. Try again, and if it keeps happening, share the reference with support."
      reference={digest}
      onRetry={onRetry}
    />
  );
  return withinShell ? state : <main className="flex min-h-dvh items-center justify-center">{state}</main>;
}

import { ErrorState } from "@dewiride/erp-ui/components/feedback/error-state";

export function ApplicationFailure({
  reference,
  onRetry,
}: {
  reference: string | undefined;
  onRetry: () => void;
}) {
  return (
    <main className="flex min-h-dvh items-center justify-center">
      <ErrorState
        headingLevel={1}
        focusOnMount
        title="The application failed to load"
        description="Something stopped the ERP from starting. Try again, and if it keeps happening, share the reference with support."
        reference={reference}
        onRetry={onRetry}
      />
    </main>
  );
}

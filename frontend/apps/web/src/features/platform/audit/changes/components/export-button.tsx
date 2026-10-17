"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { CardAction } from "@dewiride/erp-ui/components/ui/card";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { DownloadIcon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useId, useState, useTransition } from "react";
import { toast } from "sonner";

import { downloadFromApi } from "@/shared/api/download/download";
import { ApiError } from "@/shared/api/problem-details";
import { loginHref } from "@/shared/auth/sign-in-addresses";

import { exportFailureMessage, exportMediaType, exportPath, exportRefusal } from "./change-export";

// The refusal and the failure span the whole card header below its title rather than the button's column, so a long sentence
// never squeezes the title on a phone.
export function ExportButton({ filter, totalCount }: { filter: string | undefined; totalCount: number }) {
  const router = useRouter();
  const hydrated = useHydrated();
  const [pending, startDownload] = useTransition();
  const [failure, setFailure] = useState<string>();
  const refusalId = useId();
  const refusal = exportRefusal(totalCount);

  const download = () => {
    if (pending) return;
    // The last failure leaves the page at once rather than with the transition, so a retry that fails the same way inserts
    // its alert afresh and is announced again.
    setFailure(undefined);
    startDownload(async () => {
      try {
        await downloadFromApi(exportPath(filter), exportMediaType);
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          router.push(loginHref(`${window.location.pathname}${window.location.search}`, "session-ended"));
          return;
        }
        setFailure(exportFailureMessage(error));
        return;
      }
      toast.success("Downloaded the changes as a CSV file.");
    });
  };

  return (
    <>
      <CardAction>
        <Button
          type="button"
          variant="outline"
          disabled={!hydrated || refusal !== undefined}
          aria-disabled={pending || undefined}
          aria-busy={pending || undefined}
          aria-describedby={refusal === undefined ? undefined : refusalId}
          data-hydrating={hydrated ? undefined : ""}
          data-testid="audit-export"
          className="aria-disabled:opacity-50"
          onClick={download}
        >
          {pending ? (
            <Spinner data-icon="inline-start" aria-hidden="true" />
          ) : (
            <DownloadIcon data-icon="inline-start" aria-hidden />
          )}
          Download CSV
        </Button>
      </CardAction>
      {refusal === undefined ? null : (
        <p id={refusalId} className="col-span-full text-muted-foreground" data-testid="audit-export-refusal">
          {refusal}
        </p>
      )}
      {failure === undefined ? null : (
        <p role="alert" className="col-span-full text-destructive" data-testid="audit-export-error">
          {failure}
        </p>
      )}
    </>
  );
}

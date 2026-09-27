import { Alert, AlertDescription, AlertTitle } from "@dewiride/erp-ui/components/ui/alert";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
} from "@dewiride/erp-ui/components/ui/empty";
import { Progress } from "@dewiride/erp-ui/components/ui/progress";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { CircleAlertIcon, FileTextIcon, InfoIcon, PlusIcon } from "lucide-react";

import { Specimen, SpecimenGrid, SpecimenRow } from "../specimen";

import { ToastTriggers } from "./toast-triggers";

const badgeVariants = ["default", "secondary", "destructive", "outline", "ghost"] as const;
const progressValues = [0, 45, 100] as const;

export function FeedbackShowcase() {
  return (
    <SpecimenGrid>
      <Specimen title="Alert" description="Default and destructive, with an icon, title and description.">
        <div className="grid gap-3">
          <Alert>
            <InfoIcon aria-hidden />
            <AlertTitle>Books close on 31 March</AlertTitle>
            <AlertDescription>
              Post every entry for the financial year before the books close.
            </AlertDescription>
          </Alert>
          <Alert variant="destructive">
            <CircleAlertIcon aria-hidden />
            <AlertTitle>The invoice could not be issued</AlertTitle>
            <AlertDescription>The client&apos;s GSTIN is missing. Add it and try again.</AlertDescription>
          </Alert>
        </div>
      </Specimen>

      <Specimen title="Badge" description="Every variant.">
        <SpecimenRow>
          {badgeVariants.map((variant) => (
            <Badge key={variant} variant={variant}>
              {variant}
            </Badge>
          ))}
          <Badge variant="link" asChild>
            <a href="#feedback">link</a>
          </Badge>
        </SpecimenRow>
      </Specimen>

      <Specimen title="Progress" description="Not started, part way and complete.">
        <div className="grid gap-3">
          {progressValues.map((value) => (
            <div key={value} className="grid gap-1.5">
              <div className="flex justify-between text-caption text-muted-foreground">
                <span id={`ks-progress-${value}-label`}>Upload</span>
                <span>{value} %</span>
              </div>
              <Progress
                value={value}
                aria-labelledby={`ks-progress-${value}-label`}
                data-testid={`feedback-progress-${value}`}
              />
            </div>
          ))}
        </div>
      </Specimen>

      <Specimen title="Skeleton and spinner" description="Placeholders while content loads.">
        <div
          role="status"
          aria-label="Loading client details"
          aria-busy="true"
          className="flex items-center gap-3"
        >
          <Skeleton className="size-10 rounded-full" />
          <div className="grid flex-1 gap-2">
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-4 w-1/2" />
          </div>
        </div>
        <SpecimenRow>
          <Spinner className="size-3" />
          <Spinner />
          <Spinner className="size-6" />
          <span className="flex items-center gap-2 text-body text-muted-foreground">
            <Spinner />
            Refreshing
          </span>
        </SpecimenRow>
      </Specimen>

      <Specimen title="Empty" description="An empty state with an action.">
        <Empty className="border">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <FileTextIcon aria-hidden />
            </EmptyMedia>
            <EmptyTitle>No invoices yet</EmptyTitle>
            <EmptyDescription>Invoices you issue to clients appear here.</EmptyDescription>
          </EmptyHeader>
          <EmptyContent>
            <Button type="button">
              <PlusIcon data-icon="inline-start" aria-hidden />
              New invoice
            </Button>
          </EmptyContent>
        </Empty>
      </Specimen>

      <Specimen
        title="Toast"
        description="Success, information, warning, error and a loading toast that you finish."
      >
        <ToastTriggers />
      </Specimen>
    </SpecimenGrid>
  );
}

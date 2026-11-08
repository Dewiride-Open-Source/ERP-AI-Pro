import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import { ListOrderedIcon } from "lucide-react";

import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";

import { numberingNavigation } from "../../nav";
import { listNumberedDocuments, type NumberedDocumentsRead } from "../server/queries";

import { NumberingAction } from "./numbering-action";
import type { NumberedDocument } from "./numbering-items";
import {
  noDocumentsDescription,
  noDocumentsTitle,
  noRuleText,
  notSetUpText,
  numberingDescription,
  numberingUnavailable,
  pendingFormatText,
  yearLines,
} from "./numbering-text";
import { SegmentedText } from "./segmented-text";

const documentsHeadingId = "numbered-documents-heading";

export async function NumberingOverview() {
  const { title, area } = numberingNavigation;
  const [session, read] = await Promise.all([requireSession(), listNumberedDocuments()]);
  const mayChange = mayShow(session, "platform.numbering.series.change");

  return (
    <div className="grid min-w-0 grid-cols-1 gap-section" data-testid="numbering-overview">
      <header className="grid gap-2">
        <div>
          <p className="text-eyebrow text-primary uppercase">{area.title}</p>
          <h1 className="text-title">{title}</h1>
        </div>
        <p className="max-w-prose text-body text-muted-foreground">{numberingDescription}</p>
      </header>

      <Card data-testid="numbered-documents-card">
        <CardHeader>
          <CardTitle>
            <h2 id={documentsHeadingId} className="flex items-center gap-2">
              <ListOrderedIcon className="size-4 text-muted-foreground" aria-hidden />
              Kinds of document
            </h2>
          </CardTitle>
          <CardDescription>{documentsDescription(read)}</CardDescription>
        </CardHeader>
        <CardContent>
          <NumberedDocuments read={read} mayChange={mayChange} />
        </CardContent>
      </Card>
    </div>
  );
}

function documentsDescription(read: NumberedDocumentsRead): string {
  const year = read.status === "read" ? read.documents[0]?.currentYear : undefined;
  return year === undefined
    ? "Each kind of document with its numbering, by name."
    : `Each kind of document with its numbering, by name. The current financial year is ${year}.`;
}

function NumberedDocuments({ read, mayChange }: { read: NumberedDocumentsRead; mayChange: boolean }) {
  if (read.status === "failed") {
    return (
      <div
        role="status"
        data-testid="numbering-unavailable"
        className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
      >
        <p className="font-medium">{numberingUnavailable}</p>
        <p className="mt-1 text-sm text-muted-foreground">{read.reason}</p>
      </div>
    );
  }
  if (read.documents.length === 0) {
    return (
      <Empty className="border" data-testid="numbering-empty">
        <EmptyHeader>
          <EmptyTitle>{noDocumentsTitle}</EmptyTitle>
          <EmptyDescription>{noDocumentsDescription}</EmptyDescription>
        </EmptyHeader>
      </Empty>
    );
  }
  return (
    <ul role="list" aria-labelledby={documentsHeadingId} className="grid gap-4 xl:grid-cols-2">
      {read.documents.map((document) => (
        <NumberedDocumentItem key={document.documentType} document={document} mayChange={mayChange} />
      ))}
    </ul>
  );
}

function NumberedDocumentItem({ document, mayChange }: { document: NumberedDocument; mayChange: boolean }) {
  const { title, description, rule, series } = document;
  const lines = yearLines(document);
  const pending = pendingFormatText(document);

  return (
    <li className="grid min-w-0 content-start gap-3 rounded-lg border p-4" data-testid="numbered-document">
      <div className="flex flex-wrap items-start justify-between gap-x-4 gap-y-2">
        <div className="grid min-w-0 flex-[1_1_12rem] gap-1">
          <h3 className="font-medium wrap-anywhere">{title}</h3>
          <p className="text-sm text-muted-foreground">{description}</p>
        </div>
        {mayChange && rule !== null ? <NumberingAction document={document} rule={rule} /> : null}
      </div>
      {series === null ? <p data-testid="numbered-document-status">{notSetUpText}</p> : null}
      {lines.length > 0 ? (
        <dl className="grid gap-2 text-sm" data-testid="numbered-document-years">
          {lines.map((line) => (
            <div key={line.year} className="grid gap-x-3 gap-y-0.5 sm:grid-cols-[4.5rem_minmax(0,1fr)]">
              <dt className="font-medium tabular-nums">{line.year}</dt>
              <dd className="wrap-anywhere">
                <SegmentedText segments={line.text} />
              </dd>
            </div>
          ))}
        </dl>
      ) : null}
      {pending === undefined ? null : (
        <p
          className="rounded-md bg-info/10 px-3 py-2 text-sm text-info dark:bg-info/20"
          data-testid="numbered-document-pending"
        >
          <SegmentedText segments={pending} />
        </p>
      )}
      {rule === null ? (
        <p className="text-sm text-muted-foreground" data-testid="numbered-document-no-rule">
          {noRuleText(document)}
        </p>
      ) : null}
    </li>
  );
}

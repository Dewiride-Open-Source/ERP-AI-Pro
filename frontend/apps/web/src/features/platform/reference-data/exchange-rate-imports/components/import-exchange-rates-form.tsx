"use client";

import { FormAlert } from "@dewiride/erp-ui/components/forms/form-alert";
import { SubmitButton } from "@dewiride/erp-ui/components/forms/submit-button";
import { Alert, AlertDescription, AlertTitle } from "@dewiride/erp-ui/components/ui/alert";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@dewiride/erp-ui/components/ui/table";
import { FileDropZone, type FileRejection } from "@dewiride/erp-ui/components/upload/file-drop-zone";
import { formatDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import {
  CircleAlertIcon,
  CircleCheckIcon,
  DownloadIcon,
  FileSpreadsheetIcon,
  ListChecksIcon,
} from "lucide-react";
import Link from "next/link";
import { useCallback, useId, useMemo, useRef, useState } from "react";

import { useActionForm, type FormAction } from "@/shared/forms/client/use-action-form";

import { rateText, sourceText, unitText } from "../../exchange-rates/components/exchange-rate-display";
import {
  importExchangeRatesSchema,
  type ImportExchangeRatesValues,
} from "../../exchange-rates/forms/exchange-rate.schema";
import { importExchangeRates } from "../../exchange-rates/server/actions";
import { exchangeRatesNavigation } from "../../nav";
import type { CurrencyItem, ListRead } from "../../reference-lists/components/reference-options";
import { useReferenceList } from "../../reference-lists/hooks/use-reference-list";

import {
  fileRefusalText,
  fileSummaryText,
  importButtonText,
  importedRatesHref,
  importOutcomeText,
  ratesFileColumnHelp,
  ratesFileLayout,
  withFileLines,
  type FileRefusal,
} from "./import-outcome";
import {
  maxRatesFileBytes,
  ratesTemplate,
  ratesTemplateName,
  readRatesFile,
  type RatesFile,
  type RatesFileProblem,
  type RatesFileRow,
} from "./rates-file";

type Choice =
  | { readonly status: "none" }
  | { readonly status: "reading"; readonly name: string }
  | { readonly status: "refused"; readonly message: string }
  | { readonly status: "read"; readonly name: string; readonly file: RatesFile; readonly reading: number };

type FileText = { readonly text: string } | { readonly refusal: FileRefusal };

type Outcome = { readonly recorded: number; readonly unchanged: number };

// Windows names a .csv file's type after the program that opens it, often Excel's, so the type alone cannot tell a CSV file
// from a workbook; the file's name decides, and the reading of its text decides the rest.
const csvTypes: readonly string[] = ["text/csv", "application/vnd.ms-excel", "text/plain"];

const csvName = /\.(?:csv|txt)$/i;

// No event tells a page when the browser has finished reading the file a click saves, so its address stays valid for a
// minute instead of being released at once.
const savedFileAddressLifetimeMilliseconds = 60_000;

const fileHeadingId = "exchange-rate-file-heading";

export function ImportExchangeRatesForm({ today }: { today: string }) {
  const currencies = useReferenceList("currencies");
  const known = useMemo(() => currencyCodes(currencies), [currencies]);
  const [choice, setChoice] = useState<Choice>({ status: "none" });
  const latest = useRef(0);

  // A file chosen while an earlier one is still being read replaces it, so a late read is dropped.
  const take = (file: File) => {
    latest.current += 1;
    const reading = latest.current;
    setChoice({ status: "reading", name: file.name });
    void fileText(file).then((read) => {
      if (reading !== latest.current) return;
      setChoice(
        "refusal" in read
          ? { status: "refused", message: fileRefusalText(file.name, read.refusal) }
          : {
              status: "read",
              name: file.name,
              file: readRatesFile(read.text, { today, currencies: known }),
              reading,
            },
      );
    });
  };

  const refuse = (file: File, rejection: FileRejection) => {
    latest.current += 1;
    setChoice({ status: "refused", message: fileRefusalText(file.name, rejection) });
  };

  return (
    <>
      <Card data-testid="exchange-rate-file-chooser">
        <CardHeader>
          <CardTitle>
            <h2 className="flex items-center gap-2">
              <FileSpreadsheetIcon className="size-4 text-muted-foreground" aria-hidden />
              Choose the file
            </h2>
          </CardTitle>
          <CardDescription>{ratesFileLayout}</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          <dl className="grid gap-2 text-sm sm:grid-cols-[auto_minmax(0,1fr)] sm:gap-x-4">
            {ratesFileColumnHelp.map(({ column, text }) => (
              <div key={column} className="contents">
                <dt className="font-mono font-medium">{column}</dt>
                <dd className="text-muted-foreground">{text}</dd>
              </div>
            ))}
          </dl>
          <TemplateDownload />
          <FileDropZone
            accept={csvTypes}
            maxSizeBytes={maxRatesFileBytes}
            label="Drop a CSV file here"
            hint="Or choose one. In Excel, save the sheet as CSV UTF-8 (Comma delimited)."
            onFileAccepted={take}
            onFileRejected={refuse}
          />
          <p role="status" className="text-sm" data-testid="exchange-rate-file-status">
            {statusText(choice)}
          </p>
          {choice.status === "refused" ? (
            <p role="alert" className="text-sm text-destructive" data-testid="exchange-rate-file-refusal">
              {choice.message}
            </p>
          ) : null}
        </CardContent>
      </Card>

      {choice.status === "read" ? (
        <FileCheck key={choice.reading} name={choice.name} file={choice.file} today={today} />
      ) : null}
    </>
  );
}

function FileCheck({ name, file, today }: { name: string; file: RatesFile; today: string }) {
  return (
    <Card data-testid="exchange-rate-file">
      <CardHeader>
        <CardTitle>
          <h2 id={fileHeadingId} className="flex items-center gap-2">
            <ListChecksIcon className="size-4 text-muted-foreground" aria-hidden />
            Check the file
          </h2>
        </CardTitle>
        <CardDescription className="wrap-anywhere">{name}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4">
        {file.problems.length > 0 ? <FileProblems problems={file.problems} /> : null}
        {file.rows.length > 0 ? <FileRows rows={file.rows} /> : null}
        {file.problems.length === 0 ? <SendRates rows={file.rows} today={today} /> : null}
      </CardContent>
    </Card>
  );
}

function FileProblems({ problems }: { problems: readonly RatesFileProblem[] }) {
  const titleId = useId();

  return (
    <Alert
      variant="destructive"
      role="group"
      aria-labelledby={titleId}
      data-testid="exchange-rate-file-problems"
    >
      <CircleAlertIcon aria-hidden />
      <AlertTitle id={titleId}>Problems in the file</AlertTitle>
      <AlertDescription>
        <ul className="flex list-disc flex-col gap-1 pl-4">
          {problems.map((problem, index) => (
            <li key={`${index}-${problem.text}`} className="wrap-anywhere">
              {problem.text}
            </li>
          ))}
        </ul>
      </AlertDescription>
    </Alert>
  );
}

function FileRows({ rows }: { rows: readonly RatesFileRow[] }) {
  const titleId = useId();

  return (
    <div className="grid min-w-0 gap-2">
      <h3 id={titleId} className="font-medium">
        Rates in the file
      </h3>
      <div
        role="region"
        aria-labelledby={titleId}
        tabIndex={0}
        className="max-h-96 overflow-y-auto rounded-lg border focus-ring"
        data-testid="exchange-rate-file-rows"
      >
        <Table aria-labelledby={titleId}>
          <TableHeader>
            <TableRow>
              <TableHead className="text-right">Line</TableHead>
              <TableHead>Day</TableHead>
              <TableHead>Currency</TableHead>
              <TableHead className="text-right">Rate</TableHead>
              <TableHead className="text-right">Per</TableHead>
              <TableHead>Source</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((row) => (
              <TableRow key={row.line}>
                <TableCell className="text-right tabular-nums">{row.line}</TableCell>
                <TableCell>{formatDisplayDate(row.rateDate)}</TableCell>
                <TableCell>{row.currency}</TableCell>
                <TableCell className="text-right tabular-nums">{rateText(Number(row.rate))}</TableCell>
                <TableCell className="text-right tabular-nums">{unitText(Number(row.unit))}</TableCell>
                <TableCell className="max-w-64 wrap-anywhere whitespace-normal">
                  {sourceText(row.source === "" ? null : row.source)}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}

// The file's rates are sent once all of them can be, keyed by the file chosen, so another file starts with a new form; the
// answer names each refused rate by its line, and a recorded file's outcome takes the button's place and focus.
function SendRates({ rows, today }: { rows: readonly RatesFileRow[]; today: string }) {
  const [outcome, setOutcome] = useState<Outcome>();
  const schema = useMemo(() => importExchangeRatesSchema(today), [today]);
  const lines = useMemo(() => rows.map((row) => row.line), [rows]);
  const defaultValues = useMemo(
    () => ({
      rates: rows.map(({ currency, rateDate, rate, unit, source }) => ({
        currency,
        rateDate,
        rate,
        unit,
        source,
      })),
    }),
    [rows],
  );
  const action = useCallback<FormAction<ImportExchangeRatesValues>>(
    async (previous, submission) => {
      const answer = await importExchangeRates(previous, submission);
      if (answer.status === "succeeded")
        setOutcome({ recorded: answer.recorded, unchanged: answer.unchanged });
      return withFileLines(answer, lines);
    },
    [lines],
  );
  const { pending, ready, onSubmit, alertRef, alert } = useActionForm({
    schema,
    action,
    defaultValues,
    idempotent: true,
  });

  return (
    <form
      noValidate
      method="post"
      onSubmit={onSubmit}
      aria-labelledby={fileHeadingId}
      className="grid gap-4"
      data-testid="exchange-rate-import-form"
    >
      {alert ? <FormAlert ref={alertRef} {...alert} /> : null}
      {outcome === undefined ? (
        <div>
          <SubmitButton pending={pending} ready={ready} data-testid="exchange-rate-import">
            {importButtonText(rows.length)}
          </SubmitButton>
        </div>
      ) : (
        <div
          ref={focusOnArrival}
          role="status"
          tabIndex={-1}
          className="grid gap-2 rounded-lg border p-4 outline-none"
          data-testid="exchange-rate-import-outcome"
        >
          <p className="flex items-start gap-2 font-medium">
            <CircleCheckIcon className="mt-0.5 size-4 shrink-0 text-success" aria-hidden />
            {importOutcomeText(outcome.recorded, outcome.unchanged)}
          </p>
          <Link
            href={importedRatesHref(exchangeRatesNavigation.basePath, rows)}
            prefetch={false}
            className="w-fit rounded-sm text-primary underline-offset-4 focus-ring hover:underline"
          >
            Open the rates of this file
          </Link>
        </div>
      )}
    </form>
  );
}

function TemplateDownload() {
  const hydrated = useHydrated();

  return (
    <Button
      type="button"
      variant="outline"
      className="justify-self-start"
      disabled={!hydrated}
      data-hydrating={hydrated ? undefined : ""}
      onClick={saveTemplate}
      data-testid="exchange-rate-template"
    >
      <DownloadIcon data-icon="inline-start" aria-hidden />
      Download the template
    </Button>
  );
}

function saveTemplate(): void {
  const address = URL.createObjectURL(new Blob([ratesTemplate], { type: "text/csv;charset=utf-8" }));
  const link = document.createElement("a");
  link.href = address;
  link.download = ratesTemplateName;
  link.hidden = true;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(address), savedFileAddressLifetimeMilliseconds);
}

function focusOnArrival(element: HTMLDivElement | null): void {
  element?.focus();
}

function statusText(choice: Choice): string {
  switch (choice.status) {
    case "reading":
      return `Reading ${choice.name}…`;
    case "read":
      return fileSummaryText(choice.name, choice.file);
    case "none":
    case "refused":
      return "";
  }
}

function currencyCodes(list: ListRead<CurrencyItem>): ReadonlySet<string> | undefined {
  return list.status === "read" ? new Set(list.items.map((item) => item.code)) : undefined;
}

async function fileText(file: File): Promise<FileText> {
  if (!csvName.test(file.name)) return { refusal: "not-csv" };
  let bytes: ArrayBuffer;
  try {
    bytes = await file.arrayBuffer();
  } catch {
    return { refusal: "unreadable" };
  }
  try {
    return { text: new TextDecoder("utf-8", { fatal: true }).decode(bytes) };
  } catch {
    return { refusal: "not-utf8" };
  }
}

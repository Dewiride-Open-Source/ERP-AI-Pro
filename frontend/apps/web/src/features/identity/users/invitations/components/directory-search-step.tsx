"use client";

import { FormAlert } from "@dewiride/erp-ui/components/forms/form-alert";
import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { DialogClose, DialogFooter } from "@dewiride/erp-ui/components/ui/dialog";
import { Input } from "@dewiride/erp-ui/components/ui/input";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { cn } from "@dewiride/erp-ui/lib/utils";
import { ChevronRightIcon } from "lucide-react";
import Link from "next/link";
import { useId, useLayoutEffect, useRef } from "react";

import type { DirectorySearchState } from "../hooks/use-directory-search";

import {
  alreadyInTheErp,
  directorySearchMaxLength,
  searchHint,
  searchSummary,
  type DirectoryMatch,
} from "./directory-search";

// The field takes focus whenever the step appears, on opening the dialog and on coming back from a chosen person, whose
// details form is gone by then.
export function DirectorySearchStep({
  search,
  onChoose,
  onLeave,
}: {
  search: DirectorySearchState;
  onChoose: (person: DirectoryMatch) => void;
  onLeave: () => void;
}) {
  const fieldId = useId();
  const field = useRef<HTMLInputElement>(null);
  useLayoutEffect(() => {
    field.current?.focus();
  }, []);
  const { text, reading, answer, loading } = search;
  const shown = reading.status === "ready" ? answer : undefined;
  const people = shown !== undefined && "people" in shown ? shown.people : [];
  const failure = shown !== undefined && "error" in shown ? shown : undefined;

  return (
    <div className="grid min-w-0 gap-4" data-testid="directory-search">
      <FormField
        id={fieldId}
        label="Search the company directory"
        description={searchHint}
        errors={reading.status === "refused" ? [reading.message] : undefined}
      >
        {(frame) => (
          <Input
            {...frame}
            ref={field}
            type="search"
            value={text}
            onChange={(event) => search.search(event.target.value)}
            maxLength={directorySearchMaxLength}
            autoComplete="off"
            spellCheck={false}
          />
        )}
      </FormField>

      <p
        role="status"
        className="flex min-h-5 items-center gap-2 text-sm text-muted-foreground"
        data-testid="directory-search-status"
      >
        {loading && reading.status === "ready" ? <Spinner aria-hidden="true" /> : null}
        {searchSummary(reading, search)}
      </p>

      {failure ? (
        <FormAlert key={search.answerKey} title={failure.error} messages={[]} reference={failure.reference} />
      ) : null}

      {people.length > 0 ? (
        <ul
          aria-label="People in the directory"
          aria-busy={loading || undefined}
          className={cn(
            "grid min-w-0 gap-2 transition-opacity duration-(--motion-duration-fast) ease-standard",
            loading && "opacity-60",
          )}
          data-testid="directory-results"
        >
          {people.map((person) => (
            <li key={person.entraObjectId} className="min-w-0">
              {person.personId === undefined ? (
                <ChoosablePerson person={person} onChoose={onChoose} />
              ) : (
                <RegisteredPerson person={person} personId={person.personId} onLeave={onLeave} />
              )}
            </li>
          ))}
        </ul>
      ) : null}

      <DialogFooter>
        <DialogClose asChild>
          <Button type="button" variant="outline">
            Cancel
          </Button>
        </DialogClose>
      </DialogFooter>
    </div>
  );
}

function ChoosablePerson({
  person,
  onChoose,
}: {
  person: DirectoryMatch;
  onChoose: (person: DirectoryMatch) => void;
}) {
  return (
    <button
      type="button"
      onClick={() => onChoose(person)}
      className="flex w-full min-w-0 items-center gap-3 rounded-lg border px-3 py-2.5 text-left text-sm focus-ring transition-colors hover:bg-muted"
    >
      <span className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="font-medium wrap-anywhere">{person.displayName}</span>
        <span className="wrap-anywhere text-muted-foreground">{person.signInName}</span>
      </span>
      <ChevronRightIcon aria-hidden className="size-4 shrink-0 text-muted-foreground" />
    </button>
  );
}

// A person the ERP already holds is admitted with that record at their sign-in, so the dialog leads to the record instead.
function RegisteredPerson({
  person,
  personId,
  onLeave,
}: {
  person: DirectoryMatch;
  personId: string;
  onLeave: () => void;
}) {
  return (
    <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1.5 rounded-lg border border-dashed px-3 py-2.5 text-sm">
      <span className="flex min-w-0 flex-1 flex-col gap-0.5">
        <Link
          href={`/identity/users/${personId}`}
          onClick={onLeave}
          className="w-fit rounded-sm font-medium wrap-anywhere text-primary underline-offset-4 focus-ring hover:underline"
        >
          {person.displayName}
        </Link>
        <span className="wrap-anywhere text-muted-foreground">{person.signInName}</span>
      </span>
      <Badge variant="secondary">{alreadyInTheErp}</Badge>
    </div>
  );
}

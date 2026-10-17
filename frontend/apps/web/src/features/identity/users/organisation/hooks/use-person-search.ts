import type { ComboboxOption } from "@dewiride/erp-ui/components/combobox/combobox";
import { unstable_isUnrecognizedActionError, unstable_rethrow } from "next/navigation";
import { useEffect, useState, useTransition } from "react";

import { rejectedActionState } from "@/shared/forms/client/rejected-action";

import { personSearchSequence, type PauseTimer } from "../components/person-search-sequence";

export type PersonSearch = { readonly options: readonly ComboboxOption[] } | { readonly error: string };

export interface PersonSearchState {
  readonly text: string;
  readonly options: readonly ComboboxOption[];
  readonly loading: boolean;
  readonly failure: string | undefined;
  readonly search: (text: string) => void;
  readonly start: () => void;
}

type Found = { readonly options: readonly ComboboxOption[]; readonly failure: string | undefined };

const browserTimer: PauseTimer<ReturnType<typeof setTimeout>> = {
  set: (run, delay) => setTimeout(run, delay),
  clear: (handle) => clearTimeout(handle),
};

export function usePersonSearch(find: (text: string) => Promise<PersonSearch>): PersonSearchState {
  const [text, setText] = useState("");
  const [found, setFound] = useState<Found>({ options: [], failure: undefined });
  const [waiting, setWaiting] = useState(false);
  const [searching, startSearch] = useTransition();
  const [sequence] = useState(() => personSearchSequence(browserTimer));

  useEffect(() => () => sequence.stop(), [sequence]);

  const send = (query: string) => {
    const ticket = sequence.ticket();
    startSearch(async () => {
      let answer: PersonSearch;
      try {
        answer = await find(query);
      } catch (error) {
        unstable_rethrow(error);
        answer = { error: rejectedActionState(error, unstable_isUnrecognizedActionError).message };
      }
      if (!sequence.isLatest(ticket)) return;
      setFound(
        "options" in answer
          ? { options: answer.options, failure: undefined }
          : { options: [], failure: answer.error },
      );
    });
  };

  const search = (next: string) => {
    setText(next);
    setWaiting(true);
    sequence.afterPause(() => {
      setWaiting(false);
      send(next);
    });
  };

  const start = () => {
    if (!sequence.started()) send(text);
  };

  return {
    text,
    options: found.options,
    loading: waiting || searching,
    failure: found.failure,
    search,
    start,
  };
}

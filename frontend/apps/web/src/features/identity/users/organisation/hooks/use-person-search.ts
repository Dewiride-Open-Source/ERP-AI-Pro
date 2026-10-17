import type { ComboboxOption } from "@dewiride/erp-ui/components/combobox/combobox";
import { unstable_isUnrecognizedActionError, unstable_rethrow } from "next/navigation";
import { useEffect, useRef, useState, useTransition } from "react";

import { rejectedActionState } from "@/shared/forms/client/rejected-action";

export type PersonSearch = { readonly options: readonly ComboboxOption[] } | { readonly error: string };

export interface PersonSearchState {
  readonly text: string;
  readonly options: readonly ComboboxOption[];
  readonly loading: boolean;
  readonly failure: string | undefined;
  readonly search: (text: string) => void;
  readonly start: () => void;
}

const typingPause = 250;

type Found = { readonly options: readonly ComboboxOption[]; readonly failure: string | undefined };

// Next.js sends a page's Server Function calls one at a time, so the search waits until typing pauses, and an answer that
// arrives after a later search was sent is dropped, so the options always match the text in the field.
export function usePersonSearch(find: (text: string) => Promise<PersonSearch>): PersonSearchState {
  const [text, setText] = useState("");
  const [found, setFound] = useState<Found>({ options: [], failure: undefined });
  const [waiting, setWaiting] = useState(false);
  const [searching, startSearch] = useTransition();
  const pause = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const sent = useRef(0);

  useEffect(() => () => clearTimeout(pause.current), []);

  const send = (query: string) => {
    sent.current += 1;
    const ticket = sent.current;
    startSearch(async () => {
      let answer: PersonSearch;
      try {
        answer = await find(query);
      } catch (error) {
        unstable_rethrow(error);
        answer = { error: rejectedActionState(error, unstable_isUnrecognizedActionError).message };
      }
      if (ticket !== sent.current) return;
      setFound(
        "options" in answer
          ? { options: answer.options, failure: undefined }
          : { options: [], failure: answer.error },
      );
    });
  };

  const search = (next: string) => {
    setText(next);
    clearTimeout(pause.current);
    setWaiting(true);
    pause.current = setTimeout(() => {
      setWaiting(false);
      send(next);
    }, typingPause);
  };

  const start = () => {
    if (sent.current === 0 && pause.current === undefined) send(text);
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

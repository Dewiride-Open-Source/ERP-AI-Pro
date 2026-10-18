import { unstable_isUnrecognizedActionError, unstable_rethrow } from "next/navigation";
import { useEffect, useState, useTransition } from "react";

import { rejectedActionState } from "@/shared/forms/client/rejected-action";

import { personSearchSequence, type PauseTimer } from "../../organisation/components/person-search-sequence";
import {
  readSearchText,
  type DirectorySearchAnswer,
  type SearchProgress,
  type SearchText,
} from "../components/directory-search";

export interface DirectorySearchState extends SearchProgress {
  readonly text: string;
  readonly reading: SearchText;
  readonly search: (text: string) => void;
  readonly again: () => void;
}

const browserTimer: PauseTimer<ReturnType<typeof setTimeout>> = {
  set: (run, delay) => setTimeout(run, delay),
  clear: (handle) => clearTimeout(handle),
};

// Text the API would refuse is never sent, and taking a ticket for it drops the answer of a search already on its way, so
// the people shown always belong to the text in the field.
export function useDirectorySearch(
  find: (text: string) => Promise<DirectorySearchAnswer>,
): DirectorySearchState {
  const [text, setText] = useState("");
  const [answer, setAnswer] = useState<DirectorySearchAnswer | undefined>(undefined);
  const [waiting, setWaiting] = useState(false);
  const [searching, startSearch] = useTransition();
  const [sequence] = useState(() => personSearchSequence(browserTimer));

  useEffect(() => () => sequence.stop(), [sequence]);

  const send = (query: string) => {
    const ticket = sequence.ticket();
    startSearch(async () => {
      let found: DirectorySearchAnswer;
      try {
        found = await find(query);
      } catch (error) {
        unstable_rethrow(error);
        const failed = rejectedActionState(error, unstable_isUnrecognizedActionError);
        found = { error: failed.message, reference: failed.reference };
      }
      if (sequence.isLatest(ticket)) setAnswer(found);
    });
  };

  const search = (next: string) => {
    setText(next);
    const reading = readSearchText(next);
    if (reading.status !== "ready") {
      sequence.stop();
      sequence.ticket();
      setWaiting(false);
      setAnswer(undefined);
      return;
    }
    setWaiting(true);
    sequence.afterPause(() => {
      setWaiting(false);
      send(reading.text);
    });
  };

  const again = () => {
    const reading = readSearchText(text);
    if (reading.status !== "ready") return;
    sequence.stop();
    setWaiting(false);
    send(reading.text);
  };

  return { text, reading: readSearchText(text), answer, loading: waiting || searching, search, again };
}

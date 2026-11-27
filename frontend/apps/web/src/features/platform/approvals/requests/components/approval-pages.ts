import type { Route } from "next";

import type { RequestBox } from "../lists/requests.list.ts";

export type ApprovalPageKey = RequestBox | "kinds";

export interface EmptyText {
  readonly title: string;
  readonly description: string;
}

export interface ApprovalPage {
  readonly key: ApprovalPageKey;
  readonly path: Route;
  readonly title: string;
  readonly description: string;
  readonly listHeading: string;
  readonly listDescription: string;
  readonly loadingLabel: string;
  readonly empty: EmptyText;
}

export interface ApprovalPageLink {
  readonly key: ApprovalPageKey;
  readonly path: Route;
  readonly title: string;
}

const newestFirst = "The newest first. Times are in India Standard Time.";

export const requestBoxPages: Readonly<Record<RequestBox, ApprovalPage>> = {
  waiting: {
    key: "waiting",
    path: "/platform/approvals",
    title: "Waiting for me",
    description:
      "The requests that wait for your decision, because they wait for you or for a role you hold.",
    listHeading: "Requests waiting for you",
    listDescription:
      "Late requests come first. Each step has a number of working days, Monday to Friday, which end at midnight India time.",
    loadingLabel: "Loading the requests waiting for you",
    empty: {
      title: "Nothing waits for your decision.",
      description: "A request that waits for you, or for a role you hold, appears here.",
    },
  },
  mine: {
    key: "mine",
    path: "/platform/approvals/mine",
    title: "My requests",
    description:
      "The requests you asked for, and those you submitted for someone else, with where each one stands.",
    listHeading: "Requests you asked for",
    listDescription: newestFirst,
    loadingLabel: "Loading your requests",
    empty: {
      title: "You have not asked for approval yet.",
      description: "A request you ask for, or submit for someone else, appears here.",
    },
  },
  decided: {
    key: "decided",
    path: "/platform/approvals/decided",
    title: "Decided by me",
    description: "The requests with a step you approved, sent back or rejected.",
    listHeading: "Requests you decided",
    listDescription: newestFirst,
    loadingLabel: "Loading the requests you decided",
    empty: {
      title: "You have not decided a request yet.",
      description: "A request you approve, send back or reject appears here.",
    },
  },
  all: {
    key: "all",
    path: "/platform/approvals/all",
    title: "All requests",
    description: "Every approval request, whoever asked for it and whoever decides it.",
    listHeading: "Every request",
    listDescription: newestFirst,
    loadingLabel: "Loading every request",
    empty: {
      title: "Nobody has asked for approval yet.",
      description: "Every request anyone asks for appears here.",
    },
  },
};

export const kindsPage: ApprovalPage = {
  key: "kinds",
  path: "/platform/approvals/kinds",
  title: "Kinds of request",
  description:
    "Each kind of request the ERP asks approval for, with the steps it goes through and the working days each step has.",
  listHeading: "Kinds the ERP asks approval for",
  listDescription:
    "Working days are Monday to Friday and end at midnight India time. A change applies to the requests asked for after it; a request already waiting keeps its steps and its due times.",
  loadingLabel: "Loading the kinds of request",
  empty: {
    title: "No part of the ERP asks for approval yet.",
    description: "A kind of request appears here once a part of the ERP that asks for approval is in use.",
  },
};

// Every person who reads requests sees their own pages and the kinds; every request is shown only to a person who may see
// them all, so the link to it is left out for anyone else.
export function approvalPageLinks(seesAll: boolean): readonly ApprovalPageLink[] {
  const { waiting, mine, decided, all } = requestBoxPages;
  const pages = seesAll ? [waiting, mine, decided, kindsPage, all] : [waiting, mine, decided, kindsPage];
  return pages.map(({ key, path, title }) => ({ key, path, title }));
}

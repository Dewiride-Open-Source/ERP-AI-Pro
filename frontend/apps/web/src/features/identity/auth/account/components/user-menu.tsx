"use client";

import { Avatar, AvatarFallback } from "@dewiride/erp-ui/components/ui/avatar";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@dewiride/erp-ui/components/ui/dropdown-menu";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { ChevronDownIcon, LogOutIcon, UserIcon } from "lucide-react";
import { useState } from "react";

import { useSignOutForm } from "../hooks/use-sign-out-form";

export type MenuPerson = { readonly name: string; readonly userName: string; readonly initials: string };

// The sign-out form stays outside the menu, because the menu unmounts its items as it closes and a removed form cannot be
// sent. While the sign-out is under way the menu stays shut and the button keeps its focus until the page leaves. The
// menu takes its own name: Chrome would name it after the button's whole content, the hidden initials included.
export function UserMenu({ person }: { person: MenuPerson | undefined }) {
  const hydrated = useHydrated();
  const [open, setOpen] = useState(false);
  const { pending, formProps, tokenFieldProps, signOut } = useSignOutForm();
  const name = person?.name ?? "";
  const initials = person?.initials ?? "";

  return (
    <>
      <DropdownMenu open={open} onOpenChange={(next) => setOpen(next && !pending)}>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size="sm"
            disabled={!hydrated}
            aria-disabled={pending || undefined}
            aria-busy={pending || undefined}
            data-hydrating={hydrated ? undefined : ""}
            className="gap-2 px-1.5"
          >
            {pending ? (
              <Spinner aria-hidden />
            ) : (
              <Avatar size="sm" aria-hidden>
                <AvatarFallback>
                  {initials === "" ? <UserIcon className="size-3.5" /> : initials}
                </AvatarFallback>
              </Avatar>
            )}
            {name === "" ? (
              <span className="sr-only">Account</span>
            ) : (
              <span className="max-w-40 truncate max-md:sr-only">
                <span className="sr-only">Signed in as </span>
                {name}
              </span>
            )}
            <ChevronDownIcon aria-hidden className="max-md:hidden" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent
          align="end"
          aria-labelledby={undefined}
          aria-label="Account"
          className="min-w-56"
        >
          <DropdownMenuLabel className="grid gap-0.5">
            <span className="truncate text-sm text-foreground">{name === "" ? "Your account" : name}</span>
            {person?.userName ? <span className="truncate font-normal">{person.userName}</span> : null}
          </DropdownMenuLabel>
          <DropdownMenuSeparator />
          <DropdownMenuItem onSelect={signOut}>
            <LogOutIcon aria-hidden />
            Sign out
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
      <form {...formProps} hidden>
        <input {...tokenFieldProps} />
      </form>
    </>
  );
}

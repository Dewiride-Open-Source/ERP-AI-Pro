"use client";

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@dewiride/erp-ui/components/ui/dialog";
import { BookUserIcon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useCallback, useState } from "react";
import { toast } from "sonner";

import { PersonActionButton, SavingDialogCornerClose } from "../../details/components/person-action-controls";
import { useSavingDialog } from "../../details/hooks/use-saving-dialog";
import { useDirectorySearch } from "../hooks/use-directory-search";
import { searchDirectory } from "../server/actions";

import type { DirectoryMatch } from "./directory-search";
import { DirectorySearchStep } from "./directory-search-step";
import { InviteDetailsForm } from "./invite-details-form";

export function InvitePersonDialog({ canAssignRoles }: { canAssignRoles: boolean }) {
  const { open, saving, setOpen, setSaving, onOpenChange } = useSavingDialog();
  const close = useCallback(() => setOpen(false), [setOpen]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogTrigger asChild>
        <PersonActionButton pending={false} icon={BookUserIcon} variant="outline" data-testid="invite-person">
          Invite from the directory
        </PersonActionButton>
      </DialogTrigger>
      <DialogContent
        showCloseButton={false}
        className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg"
      >
        <DialogHeader>
          <DialogTitle>Invite from the directory</DialogTitle>
          <DialogDescription>
            Find the person in your organisation&apos;s Microsoft Entra directory, then add their details.
            They can sign in once an administrator gives them an app role of the ERP in Microsoft Entra.
          </DialogDescription>
        </DialogHeader>
        <InviteSteps canAssignRoles={canAssignRoles} onClose={close} onSavingChange={setSaving} />
        <SavingDialogCornerClose saving={saving} />
      </DialogContent>
    </Dialog>
  );
}

// The search lives as long as the dialog, so going back from a chosen person finds the text and the people again, searched
// once more for whoever the ERP has gained meanwhile. An invited person's page is pushed after the People page, so the
// browser's Back returns to the list.
function InviteSteps({
  canAssignRoles,
  onClose,
  onSavingChange,
}: {
  canAssignRoles: boolean;
  onClose: () => void;
  onSavingChange: (saving: boolean) => void;
}) {
  const router = useRouter();
  const search = useDirectorySearch(searchDirectory);
  const [chosen, setChosen] = useState<DirectoryMatch | undefined>(undefined);
  const invited = useCallback(
    (personId: string, personName: string) => {
      onClose();
      toast.success(`Invited ${personName}.`);
      router.push(`/identity/users/${personId}`);
    },
    [onClose, router],
  );

  if (chosen === undefined) {
    return <DirectorySearchStep search={search} onChoose={setChosen} onLeave={onClose} />;
  }
  return (
    <InviteDetailsForm
      key={chosen.entraObjectId}
      person={chosen}
      canAssignRoles={canAssignRoles}
      onInvited={invited}
      onBack={() => {
        setChosen(undefined);
        search.again();
      }}
      onSavingChange={onSavingChange}
    />
  );
}

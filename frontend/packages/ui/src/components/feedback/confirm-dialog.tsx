"use client";

import { useRef, type ReactElement, type ReactNode } from "react";

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@dewiride/erp-ui/components/ui/alert-dialog";

export type ConfirmDialogProps = {
  trigger: ReactElement;
  title: string;
  description: ReactNode;
  confirmLabel: string;
  cancelLabel?: string | undefined;
  tone?: "default" | "destructive" | undefined;
  onConfirm: () => void;
  focusAfterConfirm?: (() => HTMLElement | null) | undefined;
};

export function ConfirmDialog({
  trigger,
  title,
  description,
  confirmLabel,
  cancelLabel = "Cancel",
  tone = "default",
  onConfirm,
  focusAfterConfirm,
}: ConfirmDialogProps) {
  const confirmed = useRef(false);

  const confirm = () => {
    confirmed.current = true;
    onConfirm();
  };

  const moveFocusAfterConfirm = (event: Event) => {
    if (!confirmed.current) return;
    confirmed.current = false;
    const target = focusAfterConfirm?.();
    if (!target) return;
    event.preventDefault();
    target.focus();
  };

  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>{trigger}</AlertDialogTrigger>
      <AlertDialogContent onCloseAutoFocus={moveFocusAfterConfirm}>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{cancelLabel}</AlertDialogCancel>
          <AlertDialogAction variant={tone === "destructive" ? "destructive" : "default"} onClick={confirm}>
            {confirmLabel}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

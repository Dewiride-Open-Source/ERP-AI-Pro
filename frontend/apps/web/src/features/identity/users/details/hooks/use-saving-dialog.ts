import { useLayoutEffect, useState } from "react";

// While the dialog's form saves, Escape, a click outside, the close button and Cancel leave the dialog open, so the answer
// always reaches the form that sent it; a save that succeeds closes the dialog itself through setOpen.
export function useSavingDialog() {
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);

  return {
    open,
    saving,
    setOpen,
    setSaving,
    onOpenChange: (next: boolean) => {
      if (next || !saving) setOpen(next);
    },
  };
}

// The dialog learns that a save has started in the frame the form first shows it, before any key or click can reach the
// dialog, and that it has ended when the form goes.
export function useSavingReport(pending: boolean, report: (saving: boolean) => void): void {
  useLayoutEffect(() => {
    report(pending);
    return () => report(false);
  }, [pending, report]);
}

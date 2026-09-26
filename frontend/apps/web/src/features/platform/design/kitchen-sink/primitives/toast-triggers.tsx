"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { toast } from "sonner";

const exportToastId = "kitchen-sink-export";

export function ToastTriggers() {
  return (
    <div className="flex flex-wrap gap-3">
      <Button
        type="button"
        variant="outline"
        data-testid="toast-trigger-success"
        onClick={() =>
          toast.success("Invoice sent", { description: "INV-2026-00042 was emailed to the client." })
        }
      >
        Success
      </Button>
      <Button
        type="button"
        variant="outline"
        data-testid="toast-trigger-info"
        onClick={() =>
          toast.info("Rates updated", { description: "GST rates changed on 22 September 2026." })
        }
      >
        Info
      </Button>
      <Button
        type="button"
        variant="outline"
        data-testid="toast-trigger-warning"
        onClick={() =>
          toast.warning("Due date passed", {
            description: "Three invoices are overdue by more than 30 days.",
          })
        }
      >
        Warning
      </Button>
      <Button
        type="button"
        variant="outline"
        data-testid="toast-trigger-error"
        onClick={() => toast.error("Payment failed", { description: "The bank declined the transfer." })}
      >
        Error
      </Button>
      <Button
        type="button"
        variant="outline"
        data-testid="toast-trigger-loading"
        onClick={() =>
          toast.loading("Preparing the export", {
            id: exportToastId,
            description: "Stays open until the work finishes.",
            action: {
              label: "Finish",
              onClick: (event) => {
                event.preventDefault();
                toast.success("Export ready", {
                  id: exportToastId,
                  description: "The file is ready to download.",
                  action: null,
                });
              },
            },
          })
        }
      >
        Loading
      </Button>
    </div>
  );
}

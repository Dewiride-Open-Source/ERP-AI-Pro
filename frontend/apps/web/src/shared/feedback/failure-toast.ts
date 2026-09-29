import { toast } from "sonner";

// A failure can carry a reference to quote to support, so its toast stays until the person closes it instead of timing out
// (WCAG 2.2.1); a success toast times out, because what it reports is already on the page.
export function showFailureToast(title: string, message: string, reference?: string | undefined): void {
  toast.error(title, {
    description: reference === undefined ? message : `${message} Reference: ${reference}`,
    duration: Number.POSITIVE_INFINITY,
    closeButton: true,
  });
}

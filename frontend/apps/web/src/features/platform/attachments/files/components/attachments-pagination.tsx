import { Button } from "@dewiride/erp-ui/components/ui/button";
import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import Link from "next/link";

export function AttachmentsPagination({ page, totalPages }: { page: number; totalPages: number }) {
  if (totalPages <= 1) return null;

  return (
    <nav
      aria-label="Attachment pages"
      className="flex items-center justify-between gap-4"
      data-testid="attachments-pagination"
    >
      <PageLink page={page - 1} disabled={page <= 1} direction="previous" />
      <p className="text-sm text-muted-foreground" aria-current="page">
        Page {page} of {totalPages}
      </p>
      <PageLink page={page + 1} disabled={page >= totalPages} direction="next" />
    </nav>
  );
}

function PageLink({
  page,
  disabled,
  direction,
}: {
  page: number;
  disabled: boolean;
  direction: "previous" | "next";
}) {
  const label = direction === "previous" ? "Previous" : "Next";
  const content = (
    <>
      {direction === "previous" ? <ChevronLeftIcon aria-hidden /> : null}
      {label}
      {direction === "next" ? <ChevronRightIcon aria-hidden /> : null}
    </>
  );

  if (disabled) {
    return (
      <Button variant="outline" size="sm" disabled>
        {content}
      </Button>
    );
  }

  return (
    <Button variant="outline" size="sm" asChild>
      <Link
        href={{ pathname: "/platform/attachments", query: { page } }}
        data-testid={`attachments-${direction}`}
      >
        {content}
      </Link>
    </Button>
  );
}

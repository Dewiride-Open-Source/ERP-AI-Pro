import type { PersonStatus } from "@dewiride/erp-api-client";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";

import { statusLabel } from "./person-text";

export function PersonStatusBadge({ status, testId }: { status: PersonStatus; testId?: string | undefined }) {
  return status === "active" ? (
    <Badge
      variant="outline"
      className="border-transparent bg-success/10 text-success dark:bg-success/20"
      data-testid={testId}
    >
      {statusLabel(status)}
    </Badge>
  ) : (
    <Badge variant="secondary" data-testid={testId}>
      {statusLabel(status)}
    </Badge>
  );
}

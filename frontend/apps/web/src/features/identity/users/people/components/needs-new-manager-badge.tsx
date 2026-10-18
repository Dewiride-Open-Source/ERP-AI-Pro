import { Badge } from "@dewiride/erp-ui/components/ui/badge";

import { needsNewManager } from "./person-text";

export function NeedsNewManagerBadge() {
  return (
    <Badge
      variant="outline"
      className="border-transparent bg-warning/10 text-warning dark:bg-warning/20"
      data-testid="needs-new-manager"
    >
      {needsNewManager}
    </Badge>
  );
}

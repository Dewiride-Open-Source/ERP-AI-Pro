import type { ParameterSetBasis } from "@dewiride/erp-api-client";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";

import { basisLabel } from "./set-text";

// The ERP's own reading of the law is set apart from a government table in colour as well as in words, so a person never
// takes it for a table a notification prints.
export function SetBasisBadge({ basis }: { basis: ParameterSetBasis }) {
  return basis === "interpretation" ? (
    <Badge
      variant="outline"
      className="border-transparent bg-info/10 text-info dark:bg-info/20"
      data-testid="parameter-set-basis"
    >
      {basisLabel(basis)}
    </Badge>
  ) : (
    <Badge variant="secondary" data-testid="parameter-set-basis">
      {basisLabel(basis)}
    </Badge>
  );
}

import { fontFamilies, fontWeights, tabularAmounts, typeRoles } from "./token-catalogue";

export function TypographyScale() {
  return (
    <div className="grid min-w-0 gap-section">
      <div className="grid min-w-0 gap-3">
        <h3 className="text-body font-medium">Families and weights</h3>
        <ul className="grid gap-3">
          {fontFamilies.map((family) => (
            <li
              key={family.name}
              data-testid={`font-family-${family.name}`}
              className="grid min-w-0 gap-2 rounded-xl border bg-card p-4"
            >
              <p className="text-caption text-muted-foreground">
                <code className="font-mono">{family.className}</code> · {family.use}
              </p>
              <div className="flex flex-wrap gap-x-6 gap-y-2">
                {fontWeights.map((weight) => (
                  <p key={weight.value} className={`${family.className} ${weight.className} text-heading`}>
                    Aa {weight.value}
                  </p>
                ))}
              </div>
            </li>
          ))}
        </ul>
      </div>

      <div className="grid min-w-0 gap-3">
        <h3 className="text-body font-medium">Roles</h3>
        <ul className="grid gap-3">
          {typeRoles.map((role) => (
            <li
              key={role.role}
              data-testid={`type-role-${role.role}`}
              className="grid min-w-0 gap-2 rounded-xl border bg-card p-4"
            >
              <p className={`${role.className} wrap-break-word`}>{role.sample}</p>
              <p className="text-caption text-muted-foreground">
                <code className="font-mono">{role.utility}</code> · {role.size} / {role.lineHeight} · tracking{" "}
                {role.tracking} · weight {role.weight} · {role.use}
              </p>
            </li>
          ))}
        </ul>
      </div>

      <div className="grid min-w-0 gap-3">
        <div className="grid gap-1">
          <h3 className="text-body font-medium">Tabular numerals</h3>
          <p className="text-caption text-muted-foreground">
            The sans family turns on tabular figures, so amounts line up digit for digit in columns.
          </p>
        </div>
        <ul
          data-testid="type-tabular-numerals"
          className="grid w-fit gap-1 rounded-xl border bg-card p-4 text-right text-body"
        >
          {tabularAmounts.map((amount) => (
            <li key={amount}>{amount}</li>
          ))}
        </ul>
      </div>
    </div>
  );
}

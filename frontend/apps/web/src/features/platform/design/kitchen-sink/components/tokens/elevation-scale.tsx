import { shadows } from "./token-catalogue";

export function ElevationScale() {
  return (
    <ul className="grid grid-cols-2 gap-6 rounded-xl bg-muted/40 p-6 sm:grid-cols-4">
      {shadows.map((shadow) => (
        <li
          key={shadow.name}
          data-testid={`shadow-token-${shadow.name}`}
          className={`grid min-w-0 gap-1 rounded-xl bg-card p-4 ${shadow.className}`}
        >
          <span className="text-body font-medium">{shadow.name}</span>
          <span className="text-caption text-muted-foreground">
            <code className="font-mono">{shadow.className}</code> · {shadow.tint} tint
          </span>
        </li>
      ))}
    </ul>
  );
}

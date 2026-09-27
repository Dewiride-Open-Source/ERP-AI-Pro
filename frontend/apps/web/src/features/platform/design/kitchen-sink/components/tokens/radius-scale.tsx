import { radii } from "./token-catalogue";

export function RadiusScale() {
  return (
    <ul className="grid grid-cols-2 gap-3 sm:grid-cols-4">
      {radii.map((radius) => (
        <li
          key={radius.name}
          data-testid={`radius-token-${radius.name}`}
          className="grid min-w-0 justify-items-start gap-2 rounded-xl border bg-card p-4"
        >
          <span className={`size-16 border-2 border-primary bg-primary/10 ${radius.className}`} />
          <span className="text-body font-medium">{radius.name}</span>
          <span className="text-caption text-muted-foreground">
            <code className="font-mono">{radius.className}</code> · {radius.value}
          </span>
        </li>
      ))}
    </ul>
  );
}

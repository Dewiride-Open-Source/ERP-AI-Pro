import { namedSpacing, spacingSteps } from "./token-catalogue";

export function SpacingScale() {
  return (
    <div className="grid min-w-0 grid-cols-1 gap-section lg:grid-cols-2">
      <div className="grid min-w-0 content-start gap-3 rounded-xl border bg-card p-4">
        <div className="grid gap-1">
          <h3 className="text-body font-medium">Steps</h3>
          <p className="text-caption text-muted-foreground">
            One step is 0.25rem; primitives use the numeric steps for padding and gaps.
          </p>
        </div>
        <ul className="grid gap-2">
          {spacingSteps.map((step) => (
            <li
              key={step.name}
              data-testid={`spacing-token-${step.name}`}
              className="flex items-center gap-3"
            >
              <span className="w-10 shrink-0 text-caption text-muted-foreground">{step.name}</span>
              <span className="flex items-center gap-3">
                <span className={`h-3 shrink-0 rounded-sm bg-primary ${step.className}`} />
                <span className="text-caption text-muted-foreground">{step.value}</span>
              </span>
            </li>
          ))}
        </ul>
      </div>

      <div className="grid min-w-0 content-start gap-3 rounded-xl border bg-card p-4">
        <div className="grid gap-1">
          <h3 className="text-body font-medium">Layout</h3>
          <p className="text-caption text-muted-foreground">Named tokens for page structure.</p>
        </div>
        <ul className="grid gap-4">
          {namedSpacing.map((token) => (
            <li key={token.name} data-testid={`spacing-token-${token.name}`} className="grid min-w-0 gap-1.5">
              <span className="text-body font-medium">{token.name}</span>
              <span className={`h-3 rounded-sm bg-primary ${token.className}`} />
              <span className="text-caption text-muted-foreground">
                {token.value} · <code className="font-mono">{token.utilities}</code> · {token.use}
              </span>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}

import { focusTokens, focusUtility } from "./token-catalogue";

export function FocusScale() {
  return (
    <div className="grid min-w-0 grid-cols-1 gap-4 lg:grid-cols-2">
      <div className="grid min-w-0 content-start gap-3 rounded-xl border bg-card p-4">
        <h3 className="text-body font-medium">Tokens and utility</h3>
        <dl className="grid gap-3">
          {focusTokens.map((token) => (
            <div key={token.token} className="grid min-w-0 gap-0.5">
              <dt className="font-mono text-body wrap-anywhere">{token.token}</dt>
              <dd className="text-caption text-muted-foreground">
                {token.value} · {token.use}
              </dd>
            </div>
          ))}
          <div className="grid min-w-0 gap-0.5">
            <dt className="font-mono text-body wrap-anywhere">{focusUtility.utility}</dt>
            <dd className="text-caption text-muted-foreground">{focusUtility.use}</dd>
          </div>
        </dl>
      </div>

      <div className="grid min-w-0 content-start gap-3 rounded-xl border bg-card p-4">
        <div className="grid gap-1">
          <h3 className="text-body font-medium">Sample</h3>
          <p className="text-caption text-muted-foreground">
            Press Tab to reach the link. The preview beside it draws the same outline without focus.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-6">
          <a
            href="#focus"
            className="rounded-md px-3 py-1.5 text-body font-medium text-primary underline-offset-4 focus-ring hover:underline"
          >
            Link with the focus ring
          </a>
          <span className="rounded-md px-3 py-1.5 text-body outline-(length:--focus-ring-width) outline-offset-(--focus-ring-offset) outline-ring outline-solid">
            Focus ring preview
          </span>
        </div>
      </div>
    </div>
  );
}

import { colourGroups, intentPairings, type ColourToken } from "./token-catalogue";

export function ColourSwatches() {
  return (
    <div className="grid min-w-0 gap-section">
      {colourGroups.map((group) => (
        <div key={group.id} className="grid min-w-0 gap-3">
          <div className="grid gap-1">
            <h3 className="text-body font-medium">{group.title}</h3>
            <p className="text-caption text-muted-foreground">{group.description}</p>
          </div>
          <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
            {group.tokens.map((token) => (
              <li key={token.name} className="min-w-0">
                <Swatch token={token} />
              </li>
            ))}
          </ul>
        </div>
      ))}
      <IntentPairings />
    </div>
  );
}

function Swatch({ token }: { token: ColourToken }) {
  return (
    <figure data-testid={`token-swatch-${token.name}`} data-token={token.name} className="grid gap-2">
      <div data-slot="token-swatch-fill" className={`h-14 rounded-lg border ${token.fill}`} />
      <figcaption className="grid min-w-0 gap-0.5">
        <span className="truncate text-body font-medium">{token.name}</span>
        <code className="truncate font-mono text-caption text-muted-foreground">--{token.name}</code>
      </figcaption>
    </figure>
  );
}

function IntentPairings() {
  return (
    <div className="grid min-w-0 gap-3">
      <div className="grid gap-1">
        <h3 className="text-body font-medium">Pairings</h3>
        <p className="text-caption text-muted-foreground">
          A solid fill with its foreground, a tint (10 % in light, 20 % in dark) with the colour as text, and
          the colour as text on a card. Each pairing meets 4.5:1 in both themes.
        </p>
      </div>
      <ul className="grid gap-3">
        {intentPairings.map((pairing) => (
          <li
            key={pairing.name}
            data-testid={`token-pairing-${pairing.name}`}
            className="grid min-w-0 gap-2 sm:grid-cols-4 sm:items-center"
          >
            <span className="text-body font-medium">{pairing.name}</span>
            <span className={`rounded-lg px-3 py-2 text-body font-medium ${pairing.solid}`}>
              Solid · ₹1,250.00
            </span>
            <span className={`rounded-lg px-3 py-2 text-body font-medium ${pairing.tint}`}>
              Tint · Overdue
            </span>
            <span className={`rounded-lg border bg-card px-3 py-2 text-body font-medium ${pairing.text}`}>
              Text on card
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

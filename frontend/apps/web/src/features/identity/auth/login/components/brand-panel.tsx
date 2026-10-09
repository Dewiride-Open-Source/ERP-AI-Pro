import { ClockIcon, LockKeyholeIcon, ShieldCheckIcon, type LucideIcon } from "lucide-react";

type Fact = {
  readonly icon: LucideIcon;
  readonly title: string;
  readonly text: string;
  readonly entrance: string;
};

const facts: readonly Fact[] = [
  {
    icon: ShieldCheckIcon,
    title: "One Microsoft sign-in",
    text: "Your Dewiride work account opens the ERP. There is no separate password to keep.",
    entrance: "delay-(--motion-duration-fast)",
  },
  {
    icon: ClockIcon,
    title: "Sessions that end on their own",
    text: "After a while without activity you are signed out, and the ERP warns you first.",
    entrance: "delay-(--motion-duration-normal)",
  },
  {
    icon: LockKeyholeIcon,
    title: "Files kept private",
    text: "Attachments are encrypted before they are stored.",
    entrance: "delay-(--motion-duration-slow)",
  },
];

export function BrandPanel() {
  return (
    <div className="hidden gap-8 lg:order-first lg:grid" data-testid="sign-in-brand">
      <div className="grid animate-fade-up gap-3">
        <p className="text-eyebrow text-primary uppercase">Dewiride Technologies</p>
        <h2 className="text-heading text-balance">One workspace for the whole company.</h2>
      </div>
      <ul className="grid gap-5">
        {facts.map(({ icon: Icon, title, text, entrance }) => (
          <li key={title} className={`flex animate-fade-up gap-4 ${entrance}`}>
            <span className="inline-flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Icon className="size-5" aria-hidden />
            </span>
            <span className="grid gap-1">
              <span className="font-medium">{title}</span>
              <span className="text-sm text-muted-foreground">{text}</span>
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

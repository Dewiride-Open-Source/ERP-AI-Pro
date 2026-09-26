export type ColourToken = { name: string; fill: string };

export const colourGroups = [
  {
    id: "surfaces",
    title: "Surfaces",
    description: "Page, card and popover backgrounds, each with the text colour that sits on it.",
    tokens: [
      { name: "background", fill: "bg-background" },
      { name: "foreground", fill: "bg-foreground" },
      { name: "card", fill: "bg-card" },
      { name: "card-foreground", fill: "bg-card-foreground" },
      { name: "popover", fill: "bg-popover" },
      { name: "popover-foreground", fill: "bg-popover-foreground" },
    ],
  },
  {
    id: "intents",
    title: "Intents",
    description: "Brand, secondary, muted and accent fills with their foregrounds.",
    tokens: [
      { name: "primary", fill: "bg-primary" },
      { name: "primary-foreground", fill: "bg-primary-foreground" },
      { name: "secondary", fill: "bg-secondary" },
      { name: "secondary-foreground", fill: "bg-secondary-foreground" },
      { name: "muted", fill: "bg-muted" },
      { name: "muted-foreground", fill: "bg-muted-foreground" },
      { name: "accent", fill: "bg-accent" },
      { name: "accent-foreground", fill: "bg-accent-foreground" },
    ],
  },
  {
    id: "status",
    title: "Status",
    description: "Error, success, warning and information colours, strong enough to be used as text.",
    tokens: [
      { name: "destructive", fill: "bg-destructive" },
      { name: "destructive-foreground", fill: "bg-destructive-foreground" },
      { name: "success", fill: "bg-success" },
      { name: "success-foreground", fill: "bg-success-foreground" },
      { name: "warning", fill: "bg-warning" },
      { name: "warning-foreground", fill: "bg-warning-foreground" },
      { name: "info", fill: "bg-info" },
      { name: "info-foreground", fill: "bg-info-foreground" },
    ],
  },
  {
    id: "lines",
    title: "Lines and focus",
    description: "Dividers, control borders and the focus ring.",
    tokens: [
      { name: "border", fill: "bg-border" },
      { name: "input", fill: "bg-input" },
      { name: "ring", fill: "bg-ring" },
    ],
  },
  {
    id: "charts",
    title: "Charts",
    description: "The categorical series colours for charts.",
    tokens: [
      { name: "chart-1", fill: "bg-chart-1" },
      { name: "chart-2", fill: "bg-chart-2" },
      { name: "chart-3", fill: "bg-chart-3" },
      { name: "chart-4", fill: "bg-chart-4" },
      { name: "chart-5", fill: "bg-chart-5" },
    ],
  },
  {
    id: "sidebar",
    title: "Sidebar",
    description: "The surface, text, highlight and line colours of the application sidebar.",
    tokens: [
      { name: "sidebar", fill: "bg-sidebar" },
      { name: "sidebar-foreground", fill: "bg-sidebar-foreground" },
      { name: "sidebar-primary", fill: "bg-sidebar-primary" },
      { name: "sidebar-primary-foreground", fill: "bg-sidebar-primary-foreground" },
      { name: "sidebar-accent", fill: "bg-sidebar-accent" },
      { name: "sidebar-accent-foreground", fill: "bg-sidebar-accent-foreground" },
      { name: "sidebar-border", fill: "bg-sidebar-border" },
      { name: "sidebar-ring", fill: "bg-sidebar-ring" },
    ],
  },
] as const satisfies readonly {
  id: string;
  title: string;
  description: string;
  tokens: readonly ColourToken[];
}[];

export const intentPairings = [
  {
    name: "primary",
    solid: "bg-primary text-primary-foreground",
    tint: "bg-primary/10 text-primary dark:bg-primary/20",
    text: "text-primary",
  },
  {
    name: "destructive",
    solid: "bg-destructive text-destructive-foreground",
    tint: "bg-destructive/10 text-destructive dark:bg-destructive/20",
    text: "text-destructive",
  },
  {
    name: "success",
    solid: "bg-success text-success-foreground",
    tint: "bg-success/10 text-success dark:bg-success/20",
    text: "text-success",
  },
  {
    name: "warning",
    solid: "bg-warning text-warning-foreground",
    tint: "bg-warning/10 text-warning dark:bg-warning/20",
    text: "text-warning",
  },
  {
    name: "info",
    solid: "bg-info text-info-foreground",
    tint: "bg-info/10 text-info dark:bg-info/20",
    text: "text-info",
  },
] as const;

export const fontFamilies = [
  { name: "sans", className: "font-sans", use: "Interface text (Geist Sans)" },
  { name: "mono", className: "font-mono", use: "Codes, identifiers and technical values (Geist Mono)" },
  { name: "heading", className: "font-heading", use: "Card and dialog titles; follows the sans family" },
] as const;

export const fontWeights = [
  { value: "400", className: "font-normal" },
  { value: "500", className: "font-medium" },
  { value: "600", className: "font-semibold" },
  { value: "700", className: "font-bold" },
] as const;

export const typeRoles = [
  {
    role: "title",
    className: "text-title",
    utility: "text-title",
    size: "1.875rem",
    lineHeight: "2.25rem",
    tracking: "-0.025em",
    weight: "600",
    use: "Page title, one per page (h1)",
    sample: "Sales invoices",
  },
  {
    role: "heading",
    className: "text-heading",
    utility: "text-heading",
    size: "1.25rem",
    lineHeight: "1.75rem",
    tracking: "-0.025em",
    weight: "600",
    use: "Section heading outside a card (h2)",
    sample: "Outstanding receivables",
  },
  {
    role: "body",
    className: "text-body",
    utility: "text-body",
    size: "0.875rem",
    lineHeight: "1.25rem",
    tracking: "normal",
    weight: "inherited",
    use: "Dense body copy in tables, forms and cards",
    sample: "Payment is due within 30 days of the invoice date.",
  },
  {
    role: "caption",
    className: "text-caption",
    utility: "text-caption",
    size: "0.75rem",
    lineHeight: "1rem",
    tracking: "normal",
    weight: "inherited",
    use: "Captions, hints and metadata",
    sample: "Last updated 26 Sep 2026, 14:05 IST",
  },
  {
    role: "eyebrow",
    className: "text-eyebrow text-primary uppercase",
    utility: "text-eyebrow",
    size: "0.75rem",
    lineHeight: "1rem",
    tracking: "0.1em",
    weight: "600",
    use: "Label above a page title, always uppercase",
    sample: "Finance",
  },
] as const;

export const tabularAmounts = ["₹12,34,567.89", "₹98,765.00", "₹1,00,00,000.00", "₹4,321.10"] as const;

export const spacingSteps = [
  { name: "1", className: "w-1", value: "0.25rem" },
  { name: "2", className: "w-2", value: "0.5rem" },
  { name: "3", className: "w-3", value: "0.75rem" },
  { name: "4", className: "w-4", value: "1rem" },
  { name: "5", className: "w-5", value: "1.25rem" },
  { name: "6", className: "w-6", value: "1.5rem" },
  { name: "8", className: "w-8", value: "2rem" },
  { name: "10", className: "w-10", value: "2.5rem" },
  { name: "12", className: "w-12", value: "3rem" },
  { name: "16", className: "w-16", value: "4rem" },
] as const;

export const namedSpacing = [
  {
    name: "gutter",
    className: "w-gutter",
    value: "1rem, 1.5rem from sm",
    utilities: "px-gutter",
    use: "Side padding of the page",
  },
  {
    name: "section",
    className: "w-section",
    value: "1.5rem",
    utilities: "gap-section",
    use: "Gap between the sections of a page",
  },
  {
    name: "header",
    className: "w-header",
    value: "3.5rem",
    utilities: "h-header, scroll-mt-header",
    use: "Height of the app header and the offset of linked sections",
  },
  {
    name: "page",
    className: "w-full max-w-page",
    value: "72rem",
    utilities: "max-w-page",
    use: "Maximum width of the page content",
  },
] as const;

export const radii = [
  { name: "sm", className: "rounded-sm", value: "0.45rem (radius × 0.6)" },
  { name: "md", className: "rounded-md", value: "0.6rem (radius × 0.8)" },
  { name: "lg", className: "rounded-lg", value: "0.75rem (radius)" },
  { name: "xl", className: "rounded-xl", value: "1.05rem (radius × 1.4)" },
  { name: "2xl", className: "rounded-2xl", value: "1.35rem (radius × 1.8)" },
  { name: "3xl", className: "rounded-3xl", value: "1.65rem (radius × 2.2)" },
  { name: "4xl", className: "rounded-4xl", value: "1.95rem (radius × 2.6)" },
  { name: "full", className: "rounded-full", value: "Pill and circle" },
] as const;

export const shadows = [
  { name: "2xs", className: "shadow-2xs", tint: "faint" },
  { name: "xs", className: "shadow-xs", tint: "faint" },
  { name: "sm", className: "shadow-sm", tint: "standard" },
  { name: "md", className: "shadow-md", tint: "standard" },
  { name: "lg", className: "shadow-lg", tint: "standard" },
  { name: "xl", className: "shadow-xl", tint: "standard" },
  { name: "2xl", className: "shadow-2xl", tint: "strong" },
] as const;

export const layers = [
  {
    name: "sticky",
    token: "--layer-sticky",
    value: "40",
    utility: "z-(--layer-sticky)",
    use: "The app header",
  },
  {
    name: "overlay",
    token: "--layer-overlay",
    value: "50",
    utility: "z-50 in the generated primitives",
    use: "Dialogs, sheets, menus, selects, popovers, hover cards and tooltips",
  },
  {
    name: "toasts",
    token: "Set by sonner",
    value: "999999999",
    utility: "Inline style of the toast region",
    use: "Toast notifications, above everything else",
  },
] as const;

export const motionDurations = [
  {
    name: "fast",
    token: "--motion-duration-fast",
    value: "150ms",
    utility: "duration-(--motion-duration-fast)",
    use: "Default of every transition utility: hover, focus and colour changes",
  },
  {
    name: "normal",
    token: "--motion-duration-normal",
    value: "200ms",
    utility: "duration-(--motion-duration-normal)",
    use: "Toggles and short movements",
  },
  {
    name: "slow",
    token: "--motion-duration-slow",
    value: "300ms",
    utility: "duration-(--motion-duration-slow)",
    use: "Entrances such as animate-fade-up",
  },
  {
    name: "ambient",
    token: "--motion-duration-ambient",
    value: "6s",
    utility: "animate-glow",
    use: "The looping background glow of the sign-in page; plays once under reduced motion",
  },
] as const;

export const motionEasings = [
  {
    name: "standard",
    token: "--motion-ease-standard",
    value: "cubic-bezier(0.2, 0, 0, 1)",
    utility: "ease-standard",
    use: "Default curve of every transition utility",
  },
  {
    name: "enter",
    token: "--motion-ease-enter",
    value: "cubic-bezier(0.16, 1, 0.3, 1)",
    utility: "ease-enter",
    use: "Elements arriving on screen",
  },
] as const;

export type MotionDurationName = "fast" | "normal" | "slow";

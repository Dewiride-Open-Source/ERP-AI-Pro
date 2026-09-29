export function SkipLink({ targetId, label = "Skip to main content" }: { targetId: string; label?: string }) {
  return (
    <a
      href={`#${targetId}`}
      className="sr-only focus-ring focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-(--layer-overlay) focus:rounded-md focus:border focus:bg-background focus:px-4 focus:py-2 focus:text-sm focus:font-medium focus:text-foreground focus:shadow-md"
    >
      {label}
    </a>
  );
}

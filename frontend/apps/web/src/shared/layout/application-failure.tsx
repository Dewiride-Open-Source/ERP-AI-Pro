export function ApplicationFailure({ onReload }: { onReload: () => void }) {
  return (
    <>
      <h1 className="text-2xl font-semibold">The application failed to load</h1>
      <button type="button" onClick={onReload} className="rounded-md border px-4 py-2 text-sm">
        Reload
      </button>
    </>
  );
}

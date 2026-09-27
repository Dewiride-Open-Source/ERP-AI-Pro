import { NotFoundMessage } from "./not-found-message";

export function NotFoundPage() {
  return (
    <main className="flex min-h-dvh flex-col items-center justify-center">
      <NotFoundMessage />
    </main>
  );
}

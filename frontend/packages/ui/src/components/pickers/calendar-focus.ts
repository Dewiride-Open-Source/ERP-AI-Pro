// The calendar focuses its day as it mounts, before the popover's focus scope takes over. Inside a modal dialog the dialog's
// scope still holds focus at that moment and takes it back, so the popover, opening without focus inside it, hands focus to
// the calendar's day instead of to its first button. Elsewhere the day keeps focus and the popover never asks.
export function focusCalendarDay(event: Event): void {
  const content = event.currentTarget;
  if (!(content instanceof HTMLElement)) return;
  const day = content.querySelector<HTMLElement>("[role='grid'] button[tabindex='0']");
  if (day === null) return;
  event.preventDefault();
  day.focus();
}

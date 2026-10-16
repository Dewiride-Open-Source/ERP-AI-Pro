const indiaTimeZone = "Asia/Kolkata";

const indiaDateTime = new Intl.DateTimeFormat("en-IN", {
  timeZone: indiaTimeZone,
  dateStyle: "medium",
  timeStyle: "short",
});

const indiaTime = new Intl.DateTimeFormat("en-IN", {
  timeZone: indiaTimeZone,
  hour: "numeric",
  minute: "2-digit",
  timeZoneName: "short",
});

const indiaDay = new Intl.DateTimeFormat("en-IN", {
  timeZone: indiaTimeZone,
  weekday: "long",
  day: "numeric",
  month: "long",
  year: "numeric",
});

const indiaCalendarDate = new Intl.DateTimeFormat("en-IN", {
  timeZone: indiaTimeZone,
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
});

export function formatDateTimeIst(value: Date): string {
  return indiaDateTime.format(value);
}

export function formatTimeIst(value: Date): string {
  return indiaTime.format(value);
}

export function formatDayIst(value: Date): string {
  return indiaDay.format(value);
}

export function istDateKey(value: Date): string {
  const parts = indiaCalendarDate.formatToParts(value);
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((candidate) => candidate.type === type)?.value ?? "";
  return `${part("year").padStart(4, "0")}-${part("month")}-${part("day")}`;
}

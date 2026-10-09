const indiaDateTime = new Intl.DateTimeFormat("en-IN", {
  timeZone: "Asia/Kolkata",
  dateStyle: "medium",
  timeStyle: "short",
});

const indiaTime = new Intl.DateTimeFormat("en-IN", {
  timeZone: "Asia/Kolkata",
  hour: "numeric",
  minute: "2-digit",
  timeZoneName: "short",
});

export function formatDateTimeIst(value: Date): string {
  return indiaDateTime.format(value);
}

export function formatTimeIst(value: Date): string {
  return indiaTime.format(value);
}

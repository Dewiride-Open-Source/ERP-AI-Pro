const indiaDateTime = new Intl.DateTimeFormat("en-IN", {
  timeZone: "Asia/Kolkata",
  dateStyle: "medium",
  timeStyle: "short",
});

export function formatDateTimeIst(value: Date): string {
  return indiaDateTime.format(value);
}

const indiaDateTime = new Intl.DateTimeFormat("en-IN", {
  timeZone: "Asia/Kolkata",
  dateStyle: "medium",
  timeStyle: "short",
});

export function formatDateTimeIst(value: Date | string): string {
  return indiaDateTime.format(typeof value === "string" ? new Date(value) : value);
}

const units = ["KB", "MB", "GB"] as const;

const oneDecimal = new Intl.NumberFormat("en-IN", { maximumFractionDigits: 1 });

export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 1024) {
    const whole = Math.max(0, Math.floor(Number.isFinite(bytes) ? bytes : 0));
    return `${whole} ${whole === 1 ? "byte" : "bytes"}`;
  }

  let value = bytes / 1024;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }

  return `${oneDecimal.format(value)} ${units[unit]}`;
}

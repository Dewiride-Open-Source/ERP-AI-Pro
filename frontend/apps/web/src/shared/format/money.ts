const withPaise = new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR" });

const wholeRupees = new Intl.NumberFormat("en-IN", {
  style: "currency",
  currency: "INR",
  maximumFractionDigits: 0,
});

export function formatRupees(amount: number, { paise = true }: { paise?: boolean } = {}): string {
  return (paise ? withPaise : wholeRupees).format(amount);
}

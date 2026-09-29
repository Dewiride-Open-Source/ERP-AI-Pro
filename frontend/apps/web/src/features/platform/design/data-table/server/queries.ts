import "server-only";

import { isDateRange, type ListFilterValue, type ListQuery } from "@/shared/lists/list-query";

import { purchaseBillStatuses, type PurchaseBillStatus } from "../lists/purchase-bills.list";

export interface PurchaseBill {
  readonly id: string;
  readonly number: string;
  readonly supplier: string;
  readonly state: string;
  readonly billDate: string;
  readonly dueDate: string;
  readonly amount: number;
  readonly status: PurchaseBillStatus;
}

export interface PurchaseBillPage {
  readonly items: readonly PurchaseBill[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
}

const suppliers = [
  "Kaveri Traders",
  "Sahyadri Steel Works",
  "Shree Ganesh & Sons",
  "Narmada Packaging",
  "Deccan Logistics",
  "Malabar Spice Exports",
  "Aravalli Stationers",
  "Konark Electricals",
  "Thar Solar Systems",
  "Nilgiri Print House",
  "Vindhya Cement Agencies",
  "Brahmaputra Tea Estates",
] as const;

const states = [
  "Karnataka",
  "Maharashtra",
  "Tamil Nadu",
  "Gujarat",
  "Kerala",
  "Telangana",
  "West Bengal",
  "Delhi",
] as const;

const billCount = 64;

const firstBillDate = Date.UTC(2026, 3, 1);

const dayInMilliseconds = 86_400_000;

function isoDate(milliseconds: number): string {
  return new Date(milliseconds).toISOString().slice(0, 10);
}

function bill(index: number): PurchaseBill {
  const billed = firstBillDate + index * 3 * dayInMilliseconds;
  const credit = [15, 30, 45][index % 3] ?? 30;
  const rupees = ((index * 7_919) % 4_90_000) + 1_500;
  return {
    id: `pb-${String(index + 1).padStart(4, "0")}`,
    number: `PB/2026-27/${String(index + 1).padStart(4, "0")}`,
    supplier: suppliers[(index * 5) % suppliers.length] ?? suppliers[0],
    state: states[(index * 3) % states.length] ?? states[0],
    billDate: isoDate(billed),
    dueDate: isoDate(billed + credit * dayInMilliseconds),
    amount: rupees + ((index * 37) % 100) / 100,
    status: purchaseBillStatuses[(index * 7) % purchaseBillStatuses.length] ?? "draft",
  };
}

const purchaseBills: readonly PurchaseBill[] = Array.from({ length: billCount }, (_, index) => bill(index));

const supplierCollator = new Intl.Collator("en-IN", { sensitivity: "base" });

function matches(value: ListFilterValue, candidate: PurchaseBill, param: string): boolean {
  if (param === "supplier" && typeof value === "string") {
    return candidate.supplier.toLocaleLowerCase("en-IN").includes(value.toLocaleLowerCase("en-IN"));
  }
  if (param === "status" && Array.isArray(value)) return value.includes(candidate.status);
  if (param === "due" && isDateRange(value)) {
    return (
      (value.from === "" || candidate.dueDate >= value.from) &&
      (value.to === "" || candidate.dueDate <= value.to)
    );
  }
  return true;
}

function compare(left: PurchaseBill, right: PurchaseBill, field: string): number {
  switch (field) {
    case "supplier":
      return supplierCollator.compare(left.supplier, right.supplier);
    case "amount":
      return left.amount - right.amount;
    case "billDate":
      return left.billDate.localeCompare(right.billDate);
    case "dueDate":
      return left.dueDate.localeCompare(right.dueDate);
    default:
      return left.number.localeCompare(right.number);
  }
}

export function getPurchaseBills(query: ListQuery): PurchaseBillPage {
  const filtered = purchaseBills.filter((candidate) =>
    Object.entries(query.filters).every(([param, value]) => matches(value, candidate, param)),
  );
  const direction = query.sort.direction === "desc" ? -1 : 1;
  const sorted = [...filtered].sort(
    (left, right) => direction * compare(left, right, query.sort.field) || compare(left, right, "number"),
  );
  const start = (query.page - 1) * query.pageSize;
  return {
    items: sorted.slice(start, start + query.pageSize),
    page: query.page,
    pageSize: query.pageSize,
    totalCount: sorted.length,
    totalPages: Math.ceil(sorted.length / query.pageSize),
  };
}

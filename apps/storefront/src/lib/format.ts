import type { Money } from "@/lib/bff/schemas";

/**
 * Format a commercetools-style `Money` (minor units) as a localized currency
 * string. Falls back gracefully if the currency code is unusual.
 */
export function formatMoney(money: Money, locale?: string): string {
  const value = money.centAmount / 10 ** money.fractionDigits;
  try {
    return new Intl.NumberFormat(locale, {
      style: "currency",
      currency: money.currencyCode,
    }).format(value);
  } catch {
    return `${money.currencyCode} ${value.toFixed(money.fractionDigits)}`;
  }
}

/** Format an ISO date string for display (e.g. "Jun 5, 2026"). */
export function formatDate(iso: string, locale?: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return new Intl.DateTimeFormat(locale, {
    year: "numeric",
    month: "short",
    day: "numeric",
  }).format(date);
}

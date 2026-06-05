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

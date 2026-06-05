/**
 * Small helpers for reading loosely-typed Contentstack entry fields.
 *
 * BFF `content` responses expose CMS fields as `Record<string, unknown>` (the
 * entry shape varies per content type and is authored in Contentstack, not the
 * storefront). These helpers pull the first matching string/array field without
 * throwing on unexpected shapes.
 */

/** Return the first field (by candidate key) whose value is a non-empty string. */
export function pickString(
  fields: Record<string, unknown> | undefined,
  keys: string[]
): string | undefined {
  if (!fields) return undefined;
  for (const key of keys) {
    const value = fields[key];
    if (typeof value === "string" && value.trim().length > 0) return value;
  }
  return undefined;
}

/** Return the first field (by candidate key) that is an array of strings. */
export function pickStringArray(
  fields: Record<string, unknown> | undefined,
  keys: string[]
): string[] {
  if (!fields) return [];
  for (const key of keys) {
    const value = fields[key];
    if (Array.isArray(value) && value.every((v) => typeof v === "string")) {
      return value as string[];
    }
  }
  return [];
}

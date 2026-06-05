/**
 * W3C Trace Context + correlation-id helpers.
 *
 * The storefront originates (or propagates) a `traceparent` so that a request
 * can be followed storefront -> APIM -> BFF -> vendor spans in App Insights,
 * and an `x-correlation-id` for human-friendly log correlation.
 *
 * See architecture-plan.md "Observability".
 */

const TRACE_VERSION = "00";
const SAMPLED_FLAG = "01";

function randomHex(bytes: number): string {
  const arr = new Uint8Array(bytes);
  if (typeof globalThis.crypto?.getRandomValues === "function") {
    globalThis.crypto.getRandomValues(arr);
  } else {
    for (let i = 0; i < bytes; i++) arr[i] = Math.floor(Math.random() * 256);
  }
  return Array.from(arr, (b) => b.toString(16).padStart(2, "0")).join("");
}

export function newTraceId(): string {
  return randomHex(16); // 32 hex chars
}

export function newSpanId(): string {
  return randomHex(8); // 16 hex chars
}

export function newCorrelationId(): string {
  if (typeof globalThis.crypto?.randomUUID === "function") {
    return globalThis.crypto.randomUUID();
  }
  return `${newTraceId()}`;
}

/** Build a fresh `traceparent` value. */
export function newTraceparent(): string {
  return `${TRACE_VERSION}-${newTraceId()}-${newSpanId()}-${SAMPLED_FLAG}`;
}

/**
 * Given an inbound `traceparent` (if any), return the value to send onward.
 * We create a new child span id but keep the same trace id so the request joins
 * the existing trace; if there's no inbound header we start a new trace.
 */
export function propagateTraceparent(inbound?: string | null): string {
  if (!inbound) return newTraceparent();
  const parts = inbound.split("-");
  if (parts.length !== 4 || parts[1].length !== 32) return newTraceparent();
  return `${parts[0]}-${parts[1]}-${newSpanId()}-${parts[3]}`;
}

import "server-only";

import { cookies, headers } from "next/headers";

import type { RequestContext } from "@/lib/http/client";

/**
 * Build a `RequestContext` from the inbound request inside a Server Component
 * or Route Handler so the BFF/Auth calls continue the same trace and reuse the
 * correlation id. Note the typed clients use `credentials: 'include'`, which
 * does not forward cookies in a server-side `fetch`; for server reads that need
 * the session cookie, pass a `Cookie` header explicitly (see
 * `serverCookieHeader`).
 */
export async function getRequestContext(): Promise<RequestContext> {
  const h = await headers();
  return {
    traceparent: h.get("traceparent"),
    correlationId: h.get("x-correlation-id"),
  };
}

/**
 * Serialize the inbound cookies so a server-side fetch can present the
 * httpOnly session/cart cookies to the Auth/BFF hosts.
 */
export async function serverCookieHeader(): Promise<string> {
  const store = await cookies();
  return store
    .getAll()
    .map((c) => `${c.name}=${c.value}`)
    .join("; ");
}

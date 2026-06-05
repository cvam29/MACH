import "server-only";

import { z } from "zod";

import { env } from "@/lib/env";
import { propagateTraceparent } from "@/lib/http/trace";
import {
  getRequestContext,
  serverCookieHeader,
} from "@/lib/server/request-context";

/**
 * Server-side BFF reads.
 *
 * The browser-facing `BffClient` uses `credentials: 'include'`, which does NOT
 * forward cookies in a server `fetch`. For Server Components we instead build
 * the request here so we can:
 *   - continue the inbound trace (`traceparent` / `x-correlation-id`)
 *   - explicitly forward the httpOnly session/cart cookies for customer-scoped
 *     routes (e.g. `/orders/me`)
 *
 * Every helper is resilient: when the BFF is unreachable (offline dev, hosts not
 * running) or returns an unexpected payload, it resolves to `null` rather than
 * throwing — Server Components must still render an empty/notice state, never
 * crash the build or the request.
 */

function base(): string {
  return env.bffUrl.endsWith("/") ? env.bffUrl.slice(0, -1) : env.bffUrl;
}

interface ServerFetchOptions {
  /** Forward the inbound cookies (needed for `/me`-scoped routes). */
  withCookies?: boolean;
  /** Next.js cache hint; defaults to no-store for freshness. */
  revalidate?: number | false;
}

export async function serverGet<T>(
  path: string,
  schema: z.ZodType<T>,
  options: ServerFetchOptions = {}
): Promise<T | null> {
  try {
    const ctx = await getRequestContext();
    const headers: Record<string, string> = {
      accept: "application/json",
      traceparent: propagateTraceparent(ctx.traceparent),
    };
    if (ctx.correlationId) headers["x-correlation-id"] = ctx.correlationId;
    if (options.withCookies) headers.cookie = await serverCookieHeader();

    const url = `${base()}${path.startsWith("/") ? path : `/${path}`}`;
    const res = await fetch(url, {
      headers,
      ...(options.revalidate === undefined
        ? { cache: "no-store" as const }
        : { next: { revalidate: options.revalidate } }),
    });

    if (!res.ok) return null;
    const json: unknown = await res.json();
    const parsed = schema.safeParse(json);
    return parsed.success ? parsed.data : null;
  } catch {
    return null;
  }
}

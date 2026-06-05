import "server-only";

import { env } from "@/lib/env";
import { SessionSchema, type Session } from "@/lib/auth/schemas";
import { propagateTraceparent } from "@/lib/http/trace";
import {
  getRequestContext,
  serverCookieHeader,
} from "@/lib/server/request-context";

const GUEST_SESSION: Session = { authenticated: false, anonymous: false };

/**
 * Read `GET /auth/me` from the Auth host on the server, forwarding the inbound
 * cookies (so the httpOnly session cookie is presented) and trace context.
 *
 * Resilient by design: if the Auth host is unreachable (offline dev, Wave 2 not
 * wired yet) or the payload is unexpected, it returns a guest session rather
 * than throwing — the storefront shell must still render.
 */
export async function getServerSession(): Promise<Session> {
  try {
    const ctx = await getRequestContext();
    const cookie = await serverCookieHeader();

    const res = await fetch(`${normalizeBase(env.authUrl)}/auth/me`, {
      method: "GET",
      headers: {
        accept: "application/json",
        cookie,
        traceparent: propagateTraceparent(ctx.traceparent),
        ...(ctx.correlationId
          ? { "x-correlation-id": ctx.correlationId }
          : {}),
      },
      cache: "no-store",
    });

    if (!res.ok) return GUEST_SESSION;
    const json = await res.json();
    const parsed = SessionSchema.safeParse(json);
    return parsed.success ? parsed.data : GUEST_SESSION;
  } catch {
    return GUEST_SESSION;
  }
}

function normalizeBase(base: string): string {
  return base.endsWith("/") ? base.slice(0, -1) : base;
}

/**
 * Typed Auth microservice client (Mach.Auth.Functions).
 *
 * Base URL from `NEXT_PUBLIC_AUTH_URL` (default http://localhost:7070/api).
 * All endpoints set/clear httpOnly token cookies server-side; the storefront
 * only ever sees the safe JSON acks/session shapes. Cookies are forwarded with
 * `credentials: 'include'`.
 */
import { env } from "@/lib/env";
import {
  TypedHttpClient,
  type RequestContext,
} from "@/lib/http/client";
import {
  AnonymousSessionSchema,
  AuthAckSchema,
  SessionSchema,
  type AnonymousSession,
  type AuthAck,
  type LoginInput,
  type RegisterInput,
  type Session,
} from "@/lib/auth/schemas";

export class AuthClient {
  private readonly http: TypedHttpClient;

  constructor(baseUrl: string = env.authUrl) {
    this.http = new TypedHttpClient(baseUrl);
  }

  /** `GET /auth/me` — read the current session (guest vs signed-in). */
  me(ctx: RequestContext = {}): Promise<Session> {
    return this.http.request("/auth/me", SessionSchema, {
      cache: "no-store",
      ...ctx,
    });
  }

  /** `POST /auth/login` — commercetools password flow; sets cookies. */
  login(input: LoginInput, ctx: RequestContext = {}): Promise<AuthAck> {
    return this.http.request("/auth/login", AuthAckSchema, {
      method: "POST",
      body: input,
      ...ctx,
    });
  }

  /** `POST /auth/register` — creates a customer; sets cookies. */
  register(input: RegisterInput, ctx: RequestContext = {}): Promise<AuthAck> {
    return this.http.request("/auth/register", AuthAckSchema, {
      method: "POST",
      body: input,
      ...ctx,
    });
  }

  /** `POST /auth/refresh` — silent access-token refresh. */
  refresh(ctx: RequestContext = {}): Promise<AuthAck> {
    return this.http.request("/auth/refresh", AuthAckSchema, {
      method: "POST",
      ...ctx,
    });
  }

  /** `POST /auth/anonymous` — establish an anonymous (cart-capable) session. */
  anonymous(ctx: RequestContext = {}): Promise<AnonymousSession> {
    return this.http.request("/auth/anonymous", AnonymousSessionSchema, {
      method: "POST",
      ...ctx,
    });
  }

  /** `POST /auth/logout` — revoke + clear cookies. */
  logout(ctx: RequestContext = {}): Promise<AuthAck> {
    return this.http.request("/auth/logout", AuthAckSchema, {
      method: "POST",
      ...ctx,
    });
  }
}

/** Default singleton against the configured Auth base URL. */
export const auth = new AuthClient();

/**
 * Shared typed `fetch` wrapper used by the BFF and Auth clients.
 *
 * Responsibilities:
 *  - prefix a base URL
 *  - send cookies (`credentials: 'include'`) so the httpOnly session/cart
 *    cookies set by the Auth/BFF hosts are propagated
 *  - inject/propagate `traceparent` + `x-correlation-id`
 *  - validate the JSON response against a zod schema
 *  - surface a structured `HttpError`
 */
import type { z } from "zod";

import {
  newCorrelationId,
  propagateTraceparent,
} from "@/lib/http/trace";

export class HttpError extends Error {
  constructor(
    public readonly status: number,
    public readonly statusText: string,
    message: string,
    public readonly body?: unknown,
    public readonly correlationId?: string
  ) {
    super(message);
    this.name = "HttpError";
  }
}

export class ResponseValidationError extends Error {
  constructor(
    public readonly path: string,
    public readonly issues: unknown
  ) {
    super(`Response from ${path} failed schema validation`);
    this.name = "ResponseValidationError";
  }
}

export interface RequestContext {
  /** Inbound traceparent to continue (e.g. from a Server Component request). */
  traceparent?: string | null;
  /** Inbound correlation id to reuse. */
  correlationId?: string | null;
}

export interface RequestOptions<TBody = unknown> extends RequestContext {
  method?: "GET" | "POST" | "PATCH" | "PUT" | "DELETE";
  query?: Record<string, string | number | boolean | undefined | null>;
  body?: TBody;
  /** Idempotency-Key honored by POST cart/checkout routes. */
  idempotencyKey?: string;
  signal?: AbortSignal;
  /** Next.js fetch cache hint (e.g. `{ next: { revalidate: 60 } }`). */
  next?: { revalidate?: number | false; tags?: string[] };
  cache?: RequestCache;
}

function buildUrl(
  baseUrl: string,
  path: string,
  query?: RequestOptions["query"]
): string {
  const url = new URL(
    path.replace(/^\//, ""),
    baseUrl.endsWith("/") ? baseUrl : `${baseUrl}/`
  );
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null) {
        url.searchParams.set(key, String(value));
      }
    }
  }
  return url.toString();
}

export class TypedHttpClient {
  constructor(private readonly baseUrl: string) {}

  async request<TOut>(
    path: string,
    schema: z.ZodType<TOut>,
    options: RequestOptions = {}
  ): Promise<TOut> {
    const {
      method = "GET",
      query,
      body,
      idempotencyKey,
      signal,
      next,
      cache,
      traceparent,
      correlationId,
    } = options;

    const resolvedCorrelationId = correlationId ?? newCorrelationId();
    const headers: Record<string, string> = {
      accept: "application/json",
      traceparent: propagateTraceparent(traceparent),
      "x-correlation-id": resolvedCorrelationId,
    };
    if (body !== undefined) headers["content-type"] = "application/json";
    if (idempotencyKey) headers["idempotency-key"] = idempotencyKey;

    const url = buildUrl(this.baseUrl, path, query);

    const res = await fetch(url, {
      method,
      headers,
      credentials: "include",
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
      cache,
      ...(next ? { next } : {}),
    });

    if (!res.ok) {
      const errBody = await safeJson(res);
      throw new HttpError(
        res.status,
        res.statusText,
        `Request to ${path} failed with ${res.status}`,
        errBody,
        resolvedCorrelationId
      );
    }

    if (res.status === 204) {
      // Caller is responsible for using a schema that accepts undefined/void.
      return schema.parse(undefined);
    }

    const json = await res.json();
    const parsed = schema.safeParse(json);
    if (!parsed.success) {
      throw new ResponseValidationError(path, parsed.error.issues);
    }
    return parsed.data;
  }
}

async function safeJson(res: Response): Promise<unknown> {
  try {
    return await res.json();
  } catch {
    return undefined;
  }
}

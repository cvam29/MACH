/**
 * Zod schemas for the Mach.Auth.Functions surface (architecture-plan.md "Auth").
 *
 * The browser never receives raw tokens — they are set as httpOnly + Secure +
 * SameSite cookies by the Auth host. These shapes only describe the safe JSON
 * the storefront reads (session/customer identity, anonymous-session ack).
 */
import { z } from "zod";

export const CustomerSchema = z.object({
  id: z.string(),
  email: z.string().email(),
  firstName: z.string().nullable().optional(),
  lastName: z.string().nullable().optional(),
});
export type Customer = z.infer<typeof CustomerSchema>;

/**
 * `GET /auth/me` — describes who the caller is. A guest has an anonymous
 * session (cart-capable) but no customer; a signed-in user has a customer.
 */
export const SessionSchema = z.object({
  authenticated: z.boolean(),
  anonymous: z.boolean(),
  customer: CustomerSchema.nullable().optional(),
});
export type Session = z.infer<typeof SessionSchema>;

export const AnonymousSessionSchema = z.object({
  anonymousId: z.string(),
});
export type AnonymousSession = z.infer<typeof AnonymousSessionSchema>;

/** Generic ack for cookie-setting endpoints (login/register/refresh/logout). */
export const AuthAckSchema = z.object({
  ok: z.boolean(),
  customer: CustomerSchema.nullable().optional(),
});
export type AuthAck = z.infer<typeof AuthAckSchema>;

export const LoginInputSchema = z.object({
  email: z.string().email(),
  password: z.string().min(1),
});
export type LoginInput = z.infer<typeof LoginInputSchema>;

export const RegisterInputSchema = z.object({
  email: z.string().email(),
  password: z.string().min(8),
  firstName: z.string().min(1).optional(),
  lastName: z.string().min(1).optional(),
});
export type RegisterInput = z.infer<typeof RegisterInputSchema>;

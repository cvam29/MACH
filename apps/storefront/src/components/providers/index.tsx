"use client";

import * as React from "react";

import { QueryProvider } from "@/components/providers/query-provider";
import { AuthProvider } from "@/components/providers/auth-provider";
import { CartProvider } from "@/components/providers/cart-provider";
import { TelemetryProvider } from "@/components/providers/telemetry-provider";
import type { Session } from "@/lib/auth/schemas";
import type { Cart } from "@/lib/bff/schemas";

/**
 * Composes every client-side provider in one place so `layout.tsx` stays a
 * Server Component. Order matters: Query first (Auth/Cart read through it),
 * then Auth, then Cart, with Telemetry wrapping the tree so route changes are
 * tracked.
 */
export function Providers({
  children,
  initialSession,
  initialCart,
}: {
  children: React.ReactNode;
  initialSession?: Session;
  initialCart?: Cart;
}) {
  return (
    <TelemetryProvider>
      <QueryProvider>
        <AuthProvider initialSession={initialSession}>
          <CartProvider initialCart={initialCart}>{children}</CartProvider>
        </AuthProvider>
      </QueryProvider>
    </TelemetryProvider>
  );
}

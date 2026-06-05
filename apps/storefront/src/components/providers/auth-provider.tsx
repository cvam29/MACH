"use client";

import * as React from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";

import { auth } from "@/lib/auth/client";
import type { Session } from "@/lib/auth/schemas";

const GUEST_SESSION: Session = { authenticated: false, anonymous: false };

interface AuthContextValue {
  session: Session;
  isLoading: boolean;
  /** True when a customer is signed in (not just an anonymous guest). */
  isSignedIn: boolean;
  /** Re-fetch `/auth/me` (e.g. after login/logout). */
  refresh: () => Promise<void>;
}

const AuthContext = React.createContext<AuthContextValue | null>(null);

export function AuthProvider({
  children,
  initialSession,
}: {
  children: React.ReactNode;
  /** Optional server-read session to avoid a flash of guest chrome. */
  initialSession?: Session;
}) {
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["auth", "me"],
    queryFn: () => auth.me(),
    initialData: initialSession,
    staleTime: 60_000,
  });

  const session = data ?? GUEST_SESSION;

  const refresh = React.useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: ["auth", "me"] });
  }, [queryClient]);

  const value = React.useMemo<AuthContextValue>(
    () => ({
      session,
      isLoading,
      isSignedIn: session.authenticated && !!session.customer,
      refresh,
    }),
    [session, isLoading, refresh]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = React.useContext(AuthContext);
  if (!ctx) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return ctx;
}

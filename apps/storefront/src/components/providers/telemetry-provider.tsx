"use client";

import * as React from "react";

import { initAppInsights } from "@/lib/telemetry/app-insights";

/**
 * Initializes Application Insights on mount (no-op when the connection string
 * is absent). Renders children unchanged.
 */
export function TelemetryProvider({
  children,
}: {
  children: React.ReactNode;
}) {
  React.useEffect(() => {
    initAppInsights();
  }, []);

  return <>{children}</>;
}

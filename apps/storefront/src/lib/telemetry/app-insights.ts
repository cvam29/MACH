"use client";

/**
 * Application Insights web SDK bootstrap.
 *
 * Initializes from `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING`. If the
 * connection string is absent (typical for local/offline dev) this is a no-op,
 * so nothing leaves the machine. The React plugin is wired so route changes are
 * tracked via the provider. W3C distributed tracing is enabled to line up with
 * the `traceparent` the HTTP clients send. See architecture-plan.md
 * "Observability".
 */
import {
  ApplicationInsights,
  type IConfiguration,
  type IConfig,
} from "@microsoft/applicationinsights-web";
import { ReactPlugin } from "@microsoft/applicationinsights-react-js";

import { env } from "@/lib/env";

export const reactPlugin = new ReactPlugin();

let appInsights: ApplicationInsights | null = null;

export function getAppInsights(): ApplicationInsights | null {
  return appInsights;
}

export function initAppInsights(): ApplicationInsights | null {
  if (typeof window === "undefined") return null;
  if (appInsights) return appInsights;

  const connectionString = env.appInsightsConnectionString;
  if (!connectionString) {
    // No connection string -> telemetry disabled (offline-friendly no-op).
    return null;
  }

  const config: IConfiguration & IConfig = {
    connectionString,
    extensions: [reactPlugin],
    enableAutoRouteTracking: true,
    disableFetchTracking: false,
    enableCorsCorrelation: true,
    enableRequestHeaderTracking: true,
    enableResponseHeaderTracking: true,
    // Distributed tracing aligned with the W3C traceparent the BFF expects.
    distributedTracingMode: 2 /* AI_AND_W3C */,
  };

  appInsights = new ApplicationInsights({ config });
  appInsights.loadAppInsights();
  appInsights.trackPageView();
  return appInsights;
}

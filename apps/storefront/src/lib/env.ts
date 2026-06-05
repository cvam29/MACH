/**
 * Public runtime configuration.
 *
 * Only `NEXT_PUBLIC_*` values are safe to read in the browser. Server-only
 * secrets (commercetools client secret, Adyen HMAC, etc.) never live here —
 * they stay in the .NET hosts / Key Vault per the architecture plan.
 */

const DEFAULT_BFF_URL = "http://localhost:7071/api";
const DEFAULT_AUTH_URL = "http://localhost:7070/api";

export const env = {
  /** Bff.Functions base, behind APIM `/api` in cloud. */
  bffUrl: process.env.NEXT_PUBLIC_BFF_URL ?? DEFAULT_BFF_URL,
  /** Mach.Auth.Functions base. */
  authUrl: process.env.NEXT_PUBLIC_AUTH_URL ?? DEFAULT_AUTH_URL,
  /** Algolia — browser-side search uses a search-only public key. */
  algolia: {
    appId: process.env.NEXT_PUBLIC_ALGOLIA_APP_ID ?? "",
    searchKey: process.env.NEXT_PUBLIC_ALGOLIA_SEARCH_KEY ?? "",
  },
  /** Adyen Drop-in client key (public). */
  adyenClientKey: process.env.NEXT_PUBLIC_ADYEN_CLIENT_KEY ?? "",
  /** Application Insights connection string (telemetry no-ops if absent). */
  appInsightsConnectionString:
    process.env.NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING ?? "",
} as const;

export type PublicEnv = typeof env;

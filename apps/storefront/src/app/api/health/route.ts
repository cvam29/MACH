import { NextResponse } from "next/server";

/**
 * Liveness endpoint for the storefront shell itself (distinct from the BFF's
 * `/health`). Returns 200 with basic build/runtime info — used by container
 * orchestration and the CI smoke step.
 */
export const dynamic = "force-dynamic";

export function GET() {
  return NextResponse.json({
    status: "healthy",
    service: "storefront",
    timestamp: new Date().toISOString(),
  });
}

"use client";

import * as React from "react";
import { Loader2 } from "lucide-react";

import "@adyen/adyen-web/styles/adyen.css";

import { env } from "@/lib/env";
import type { PaymentSession } from "@/lib/bff/schemas";

/**
 * Adyen Drop-in (test mode). The SDK touches `window` and is heavy, so it is
 * imported dynamically (client-only) inside an effect — the parent mounts this
 * component with `next/dynamic({ ssr: false })`. The session comes from the BFF
 * (`POST /checkout/{cartId}/payment-session`, which wraps Adyen `/sessions`).
 *
 * Resilience: if the client key is missing or the SDK fails to initialise, a
 * notice is shown instead of crashing. In a real flow `onPaymentCompleted`
 * fires after the shopper pays; here we surface it to the parent so it can
 * place the order.
 */
export function AdyenDropin({
  session,
  onCompleted,
  onError,
}: {
  session: PaymentSession;
  onCompleted: (resultCode: string) => void;
  onError: (message: string) => void;
}) {
  const containerRef = React.useRef<HTMLDivElement>(null);
  const [ready, setReady] = React.useState(false);
  const [initError, setInitError] = React.useState<string | null>(null);

  const clientKey = session.clientKey || env.adyenClientKey;

  // Keep the latest callbacks without re-running the mount effect. Refs are
  // updated inside an effect (never during render).
  const onCompletedRef = React.useRef(onCompleted);
  const onErrorRef = React.useRef(onError);
  React.useEffect(() => {
    onCompletedRef.current = onCompleted;
    onErrorRef.current = onError;
  });

  React.useEffect(() => {
    if (!clientKey) return;

    let disposed = false;
    let dropinInstance: { unmount: () => void } | null = null;

    (async () => {
      try {
        const { AdyenCheckout, Dropin } = await import("@adyen/adyen-web");

        const checkout = await AdyenCheckout({
          environment: "test",
          clientKey,
          session: {
            id: session.sessionId,
            sessionData: session.sessionData,
          },
          onPaymentCompleted: (result) => {
            const resultCode =
              (result as { resultCode?: string }).resultCode ?? "Authorised";
            onCompletedRef.current(resultCode);
          },
          onError: (error) => {
            onErrorRef.current(error.message ?? "Payment error");
          },
        });

        if (disposed || !containerRef.current) return;

        const dropin = new Dropin(checkout, {
          // Surface common test methods; the BFF/Adyen config drives the rest.
          paymentMethodsConfiguration: {
            card: { hasHolderName: true, holderNameRequired: false },
          },
        });
        dropin.mount(containerRef.current);
        dropinInstance = dropin;
        setReady(true);
      } catch (err) {
        if (!disposed) {
          setInitError(
            err instanceof Error
              ? err.message
              : "Failed to initialise the payment form."
          );
        }
      }
    })();

    return () => {
      disposed = true;
      try {
        dropinInstance?.unmount();
      } catch {
        /* ignore unmount errors */
      }
    };
    // Re-init only when the session identity changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session.sessionId, clientKey]);

  // Missing client key is a static condition — derive it, don't store state.
  const fatal = !clientKey
    ? "Adyen client key is not configured."
    : initError;

  if (fatal) {
    return (
      <p role="alert" className="text-destructive text-sm">
        {fatal} Payment is unavailable — set{" "}
        <code className="font-mono">NEXT_PUBLIC_ADYEN_CLIENT_KEY</code> and run
        the BFF to enable Adyen Drop-in.
      </p>
    );
  }

  return (
    <div className="space-y-3">
      {!ready && (
        <div className="text-muted-foreground flex items-center gap-2 text-sm">
          <Loader2 className="size-4 animate-spin" /> Loading secure payment…
        </div>
      )}
      <div ref={containerRef} />
    </div>
  );
}

"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import dynamic from "next/dynamic";
import { z } from "zod";
import { Check, Loader2 } from "lucide-react";

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { DeliverySelector } from "@/components/checkout/delivery-selector";
import { useAuth } from "@/components/providers/auth-provider";
import { useCartStore } from "@/lib/cart/store";
import { bff, type AddressInput } from "@/lib/bff/client";
import { formatMoney } from "@/lib/format";
import type {
  Cart,
  DeliveryOption,
  DeliveryType,
  PaymentSession,
} from "@/lib/bff/schemas";

// Adyen Drop-in is client-only (touches window) and heavy — load on demand.
const AdyenDropin = dynamic(
  () => import("@/components/checkout/adyen-dropin").then((m) => m.AdyenDropin),
  {
    ssr: false,
    loading: () => (
      <div className="text-muted-foreground flex items-center gap-2 text-sm">
        <Loader2 className="size-4 animate-spin" /> Loading payment…
      </div>
    ),
  }
);

const AddressSchema = z.object({
  firstName: z.string().min(1, "Required"),
  lastName: z.string().min(1, "Required"),
  street: z.string().min(1, "Required"),
  city: z.string().min(1, "Required"),
  postalCode: z.string().min(1, "Required"),
  country: z.string().min(2, "Use a 2-letter country code"),
});

type Step = "address" | "delivery" | "payment";

export function CheckoutFlow({ initialCart }: { initialCart: Cart | null }) {
  const router = useRouter();
  const { isSignedIn, session } = useAuth();

  const hydrate = useCartStore((s) => s.hydrate);
  const storeCartId = useCartStore((s) => s.cartId);
  const totals = useCartStore((s) => s.totals);

  // Seed the mirror from the server cart once.
  React.useEffect(() => {
    if (initialCart) hydrate(initialCart);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const cartId = storeCartId ?? initialCart?.id ?? null;

  const [step, setStep] = React.useState<Step>("address");
  const [address, setAddress] = React.useState<AddressInput | null>(null);
  const [delivery, setDelivery] = React.useState<{
    type: DeliveryType;
    option: DeliveryOption;
  } | null>(null);
  const [paymentSession, setPaymentSession] =
    React.useState<PaymentSession | null>(null);
  const [errors, setErrors] = React.useState<Record<string, string>>({});
  const [busy, setBusy] = React.useState(false);
  const [flowError, setFlowError] = React.useState<string | null>(null);

  if (!cartId) {
    return (
      <Card className="mx-auto max-w-xl">
        <CardContent className="space-y-3 py-12 text-center">
          <p className="text-sm font-medium">Your cart is empty.</p>
          <p className="text-muted-foreground text-sm">
            Add items before checking out.
          </p>
          <Button asChild>
            <Link href="/search">Browse products</Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  async function handleAddressSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setFlowError(null);
    const form = new FormData(e.currentTarget);
    const parsed = AddressSchema.safeParse({
      firstName: form.get("firstName"),
      lastName: form.get("lastName"),
      street: form.get("street"),
      city: form.get("city"),
      postalCode: form.get("postalCode"),
      country: form.get("country"),
    });

    if (!parsed.success) {
      const fieldErrors: Record<string, string> = {};
      for (const issue of parsed.error.issues) {
        const key = issue.path[0];
        if (typeof key === "string") fieldErrors[key] = issue.message;
      }
      setErrors(fieldErrors);
      return;
    }
    setErrors({});

    setBusy(true);
    try {
      // Persist the shipping address on the commercetools cart (best effort —
      // even if the BFF is offline we still advance to show the delivery UI).
      try {
        const updated = await bff.setShippingAddress(cartId!, parsed.data);
        hydrate(updated);
      } catch {
        /* non-fatal: delivery step will surface any BFF outage */
      }
      setAddress(parsed.data);
      setStep("delivery");
    } finally {
      setBusy(false);
    }
  }

  async function handleDeliverySelect(
    type: DeliveryType,
    option: DeliveryOption
  ) {
    setDelivery({ type, option });
    setFlowError(null);
    try {
      const updated = await bff.selectDelivery(cartId!, type);
      hydrate(updated);
    } catch {
      /* non-fatal: keep the local selection so the user can proceed */
    }
  }

  async function goToPayment() {
    if (!delivery) return;
    setBusy(true);
    setFlowError(null);
    try {
      const ps = await bff.createPaymentSession(cartId!);
      setPaymentSession(ps);
      setStep("payment");
    } catch {
      setFlowError(
        "Could not start payment. The payments service (Adyen via the BFF) may be offline."
      );
    } finally {
      setBusy(false);
    }
  }

  async function handlePaymentCompleted() {
    setBusy(true);
    setFlowError(null);
    try {
      const order = await bff.placeOrder(cartId!);
      router.push(`/order/${encodeURIComponent(order.id)}`);
    } catch {
      setFlowError(
        "Payment succeeded but placing the order failed. Please check your account or contact support."
      );
      setBusy(false);
    }
  }

  return (
    <div className="grid gap-8 lg:grid-cols-[1fr_22rem]">
      <div className="space-y-4">
        {/* Step 1 — sign in or guest */}
        <StepCard
          index={1}
          title="Sign in or continue as guest"
          done={true}
          description={
            isSignedIn
              ? `Signed in as ${session.customer?.email}.`
              : "You're checking out as a guest. Your anonymous cart will convert to an order; sign in to keep your history."
          }
        >
          {!isSignedIn && (
            <div className="flex gap-2">
              <Button asChild size="sm" variant="outline">
                <Link href="/login?next=/checkout">Sign in</Link>
              </Button>
              <Button asChild size="sm" variant="ghost">
                <Link href="/register">Create account</Link>
              </Button>
            </div>
          )}
        </StepCard>

        {/* Step 2 — address */}
        <StepCard
          index={2}
          title="Shipping address"
          done={step !== "address"}
          description="Used to price delivery by distance (Azure Maps)."
        >
          {step === "address" ? (
            <form onSubmit={handleAddressSubmit} className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <Field
                  name="firstName"
                  label="First name"
                  autoComplete="given-name"
                  error={errors.firstName}
                />
                <Field
                  name="lastName"
                  label="Last name"
                  autoComplete="family-name"
                  error={errors.lastName}
                />
              </div>
              <Field
                name="street"
                label="Street address"
                autoComplete="street-address"
                error={errors.street}
              />
              <div className="grid gap-4 sm:grid-cols-3">
                <Field
                  name="city"
                  label="City"
                  autoComplete="address-level2"
                  error={errors.city}
                />
                <Field
                  name="postalCode"
                  label="Postal code"
                  autoComplete="postal-code"
                  error={errors.postalCode}
                />
                <Field
                  name="country"
                  label="Country"
                  autoComplete="country"
                  placeholder="US"
                  error={errors.country}
                />
              </div>
              <Button type="submit" disabled={busy}>
                {busy ? "Saving…" : "Continue to delivery"}
              </Button>
            </form>
          ) : (
            <div className="flex items-center justify-between gap-3">
              <p className="text-muted-foreground text-sm">
                {address?.street}, {address?.city} {address?.postalCode},{" "}
                {address?.country}
              </p>
              <Button
                size="sm"
                variant="ghost"
                onClick={() => setStep("address")}
              >
                Edit
              </Button>
            </div>
          )}
        </StepCard>

        {/* Step 3 — delivery */}
        <StepCard
          index={3}
          title="Delivery"
          done={step === "payment"}
          description="Standard / express / same-day / store-pickup, priced live by distance."
        >
          {step === "delivery" && address ? (
            <div className="space-y-4">
              <DeliverySelector
                cartId={cartId}
                address={address}
                selected={delivery?.type ?? null}
                onSelect={handleDeliverySelect}
              />
              <Button onClick={goToPayment} disabled={!delivery || busy}>
                {busy ? "Starting payment…" : "Continue to payment"}
              </Button>
            </div>
          ) : step === "payment" && delivery ? (
            <div className="flex items-center justify-between gap-3">
              <p className="text-muted-foreground text-sm">
                {delivery.option.label} · {delivery.option.eta} ·{" "}
                {delivery.option.price.centAmount === 0
                  ? "Free"
                  : formatMoney(delivery.option.price)}
              </p>
              <Button
                size="sm"
                variant="ghost"
                onClick={() => setStep("delivery")}
              >
                Change
              </Button>
            </div>
          ) : (
            <p className="text-muted-foreground text-sm">
              Enter your address to see delivery options.
            </p>
          )}
        </StepCard>

        {/* Step 4 — payment */}
        <StepCard
          index={4}
          title="Payment"
          done={false}
          description="Adyen Drop-in (test mode). Use a 3DS test card."
        >
          {step === "payment" && paymentSession ? (
            <AdyenDropin
              session={paymentSession}
              onCompleted={handlePaymentCompleted}
              onError={(message) => setFlowError(message)}
            />
          ) : (
            <p className="text-muted-foreground text-sm">
              Complete the previous steps to pay.
            </p>
          )}
          {busy && step === "payment" && (
            <p className="text-muted-foreground mt-3 flex items-center gap-2 text-sm">
              <Loader2 className="size-4 animate-spin" /> Placing your order…
            </p>
          )}
        </StepCard>

        {flowError && (
          <p role="alert" className="text-destructive text-sm">
            {flowError}
          </p>
        )}
      </div>

      {/* Order summary */}
      <Card className="h-fit">
        <CardHeader>
          <CardTitle className="text-base">Order summary</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3 text-sm">
          <div className="flex justify-between">
            <span className="text-muted-foreground">
              Subtotal ({totals.itemCount} item
              {totals.itemCount === 1 ? "" : "s"})
            </span>
            <span className="tabular-nums">
              {formatMoney(totals.subtotal)}
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Delivery</span>
            <span className="tabular-nums">
              {delivery
                ? delivery.option.price.centAmount === 0
                  ? "Free"
                  : formatMoney(delivery.option.price)
                : "—"}
            </span>
          </div>
          {totals.tax && (
            <div className="flex justify-between">
              <span className="text-muted-foreground">Tax</span>
              <span className="tabular-nums">{formatMoney(totals.tax)}</span>
            </div>
          )}
          <div className="flex justify-between border-t pt-3 font-semibold">
            <span>Total</span>
            <span className="tabular-nums">{formatMoney(totals.total)}</span>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

function StepCard({
  index,
  title,
  description,
  done,
  children,
}: {
  index: number;
  title: string;
  description?: string;
  done?: boolean;
  children?: React.ReactNode;
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-3 text-base">
          <span
            className={
              done
                ? "bg-primary text-primary-foreground inline-flex size-6 items-center justify-center rounded-full"
                : "bg-muted text-muted-foreground inline-flex size-6 items-center justify-center rounded-full text-xs font-semibold"
            }
          >
            {done ? <Check className="size-3.5" /> : index}
          </span>
          {title}
        </CardTitle>
        {description && <CardDescription>{description}</CardDescription>}
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}

function Field({
  name,
  label,
  error,
  autoComplete,
  placeholder,
}: {
  name: string;
  label: string;
  error?: string;
  autoComplete?: string;
  placeholder?: string;
}) {
  return (
    <div className="space-y-2">
      <Label htmlFor={name}>{label}</Label>
      <Input
        id={name}
        name={name}
        autoComplete={autoComplete}
        placeholder={placeholder}
        aria-invalid={!!error}
        required
      />
      {error && (
        <p className="text-destructive text-xs" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

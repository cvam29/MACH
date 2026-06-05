import type { Metadata } from "next";

import { PageShell } from "@/components/layout/page-shell";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

export const metadata: Metadata = { title: "Checkout" };

const STEPS = [
  {
    title: "Sign in or guest",
    body: "Continue as guest with an anonymous cart, or sign in to merge it.",
  },
  {
    title: "Address",
    body: "Enter a shipping address — used for distance-based delivery quoting.",
  },
  {
    title: "Delivery",
    body: "Standard / express / same-day / store-pickup, priced live by distance via Azure Maps.",
  },
  {
    title: "Payment",
    body: "Adyen Drop-in (loaded client-side, ssr:false) with a 3DS test card.",
  },
];

export default function CheckoutPage() {
  return (
    <PageShell
      title="Checkout"
      description="A four-step flow. Delivery re-prices live by distance; payment uses Adyen Drop-in. Wired end-to-end in Wave 2."
    >
      <div className="grid gap-8 lg:grid-cols-[1fr_22rem]">
        <ol className="space-y-4">
          {STEPS.map((step, i) => (
            <li key={step.title}>
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center gap-3 text-base">
                    <span className="bg-muted text-muted-foreground inline-flex size-6 items-center justify-center rounded-full text-xs font-semibold">
                      {i + 1}
                    </span>
                    {step.title}
                  </CardTitle>
                  <CardDescription>{step.body}</CardDescription>
                </CardHeader>
                <CardContent className="space-y-2">
                  <Skeleton className="h-9 w-full" />
                  <Skeleton className="h-9 w-2/3" />
                </CardContent>
              </Card>
            </li>
          ))}
        </ol>

        <Card className="h-fit">
          <CardHeader>
            <CardTitle className="text-base">Order summary</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-5/6" />
            <Skeleton className="h-4 w-1/2" />
          </CardContent>
        </Card>
      </div>
    </PageShell>
  );
}

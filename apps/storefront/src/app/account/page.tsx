import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { PageShell } from "@/components/layout/page-shell";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { getServerSession } from "@/lib/server/session";

export const metadata: Metadata = { title: "Account" };

export default async function AccountPage() {
  // Guard: a signed-in customer is required. Anonymous guests and unauthenticated
  // visitors are redirected to login with a return path. (Server-side check;
  // hardened with proxy/edge enforcement in Wave 2.)
  const session = await getServerSession();
  if (!session.authenticated || !session.customer) {
    redirect("/login?next=/account");
  }

  const customer = session.customer;

  return (
    <PageShell
      title="Your account"
      description="Profile and order history. Order history reads the SQL projection via GET /orders/me in Wave 2."
    >
      <div className="grid gap-6 lg:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Profile</CardTitle>
          </CardHeader>
          <CardContent className="space-y-1 text-sm">
            <p className="font-medium">
              {[customer.firstName, customer.lastName]
                .filter(Boolean)
                .join(" ") || "Customer"}
            </p>
            <p className="text-muted-foreground">{customer.email}</p>
          </CardContent>
        </Card>

        <Card id="orders" className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Order history</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {Array.from({ length: 3 }).map((_, i) => (
              <div
                key={i}
                className="flex items-center justify-between gap-4"
              >
                <div className="space-y-1.5">
                  <Skeleton className="h-4 w-32" />
                  <Skeleton className="h-3 w-24" />
                </div>
                <Skeleton className="h-6 w-20" />
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </PageShell>
  );
}

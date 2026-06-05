import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { Package } from "lucide-react";

import { PageShell } from "@/components/layout/page-shell";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { OrderStatusBadge } from "@/components/order/order-status-badge";
import { getServerSession } from "@/lib/server/session";
import { getMyOrders } from "@/lib/server/orders";
import { formatDate, formatMoney } from "@/lib/format";

export const metadata: Metadata = { title: "Account" };

export default async function AccountPage() {
  // Guard: a signed-in customer is required. Anonymous guests and
  // unauthenticated visitors are redirected to login with a return path.
  const session = await getServerSession();
  if (!session.authenticated || !session.customer) {
    redirect("/login?next=/account");
  }

  const customer = session.customer;
  const orders = await getMyOrders();

  return (
    <PageShell
      title="Your account"
      description="Profile and order history. Order history reads the SQL projection via GET /orders/me."
    >
      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="h-fit">
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
          <CardContent>
            {orders.length > 0 ? (
              <ul className="divide-border divide-y">
                {orders.map((order) => (
                  <li
                    key={order.id}
                    className="flex flex-wrap items-center justify-between gap-3 py-3 first:pt-0 last:pb-0"
                  >
                    <div className="space-y-0.5">
                      <Link
                        href={`/order/${encodeURIComponent(order.id)}`}
                        className="text-sm font-medium hover:underline"
                      >
                        Order #{order.orderNumber}
                      </Link>
                      <p className="text-muted-foreground text-xs">
                        {formatDate(order.createdAt)} ·{" "}
                        {order.totals.itemCount} item
                        {order.totals.itemCount === 1 ? "" : "s"}
                      </p>
                    </div>
                    <div className="flex items-center gap-3">
                      <span className="text-sm tabular-nums">
                        {formatMoney(order.totals.total)}
                      </span>
                      <OrderStatusBadge status={order.status} />
                    </div>
                  </li>
                ))}
              </ul>
            ) : (
              <div className="flex flex-col items-center gap-3 py-8 text-center">
                <Package
                  className="text-muted-foreground/50 size-8"
                  aria-hidden
                />
                <p className="text-sm font-medium">No orders yet.</p>
                <p className="text-muted-foreground text-sm">
                  When you place an order it will appear here.
                </p>
                <Button asChild variant="outline" size="sm">
                  <Link href="/search">Start shopping</Link>
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </PageShell>
  );
}

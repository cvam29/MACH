import type { Metadata } from "next";
import Link from "next/link";
import { CheckCircle2, Mail, Truck } from "lucide-react";

import { PageShell } from "@/components/layout/page-shell";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { RemoteImage } from "@/components/product/remote-image";
import { OrderStatusBadge } from "@/components/order/order-status-badge";
import { getOrderById } from "@/lib/server/orders";
import { formatMoney } from "@/lib/format";

export async function generateMetadata({
  params,
}: PageProps<"/order/[id]">): Promise<Metadata> {
  const { id } = await params;
  return { title: `Order ${id}` };
}

export default async function OrderPage({ params }: PageProps<"/order/[id]">) {
  const { id } = await params;
  const order = await getOrderById(id);

  return (
    <PageShell
      title="Order confirmed"
      description="Your order summary, the delivery you chose, and the confirmation emails that were sent."
    >
      <div className="mb-6 flex items-center gap-3">
        <CheckCircle2 className="text-primary size-6" aria-hidden />
        <p className="text-sm">
          Thank you! Your order{" "}
          <span className="font-mono font-medium">
            #{order?.orderNumber ?? id}
          </span>{" "}
          has been placed.
        </p>
        {order && <OrderStatusBadge status={order.status} />}
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Items</CardTitle>
          </CardHeader>
          <CardContent>
            {order && order.lineItems.length > 0 ? (
              <ul className="divide-border divide-y">
                {order.lineItems.map((li) => (
                  <li
                    key={li.id}
                    className="flex items-center gap-4 py-3 first:pt-0 last:pb-0"
                  >
                    <RemoteImage
                      src={li.imageUrl}
                      alt={li.name}
                      className="size-16 rounded-md border"
                      sizes="64px"
                    />
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">{li.name}</p>
                      <p className="text-muted-foreground text-xs">
                        Qty {li.quantity} · {formatMoney(li.unitPrice)}
                      </p>
                    </div>
                    <span className="text-sm tabular-nums">
                      {formatMoney(li.totalPrice)}
                    </span>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-muted-foreground text-sm">
                Order details are not available yet. They will appear once the
                BFF order projection is ready.
              </p>
            )}

            {order && (
              <div className="mt-4 flex justify-between border-t pt-4 text-sm font-semibold">
                <span>Total</span>
                <span className="tabular-nums">
                  {formatMoney(order.totals.total)}
                </span>
              </div>
            )}
          </CardContent>
        </Card>

        <div className="space-y-6">
          <Card className="h-fit">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <Truck className="size-4" aria-hidden /> Delivery
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-1 text-sm">
              {order?.delivery ? (
                <>
                  <p className="font-medium">{order.delivery.label}</p>
                  <p className="text-muted-foreground">{order.delivery.eta}</p>
                  <p className="text-muted-foreground">
                    {order.delivery.price.centAmount === 0
                      ? "Free"
                      : formatMoney(order.delivery.price)}
                  </p>
                </>
              ) : (
                <p className="text-muted-foreground">
                  Delivery details will appear shortly.
                </p>
              )}
            </CardContent>
          </Card>

          <Card className="h-fit">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <Mail className="size-4" aria-hidden /> Confirmations sent
              </CardTitle>
            </CardHeader>
            <CardContent className="text-muted-foreground space-y-1 text-sm">
              <p>
                Confirmation emails have been sent to all parties — customer,
                store, supplier and reception — with copy authored in
                Contentstack.
              </p>
            </CardContent>
          </Card>
        </div>
      </div>

      <div className="mt-8 flex gap-3">
        <Button asChild variant="outline">
          <Link href="/account#orders">View order history</Link>
        </Button>
        <Button asChild>
          <Link href="/search">Continue shopping</Link>
        </Button>
      </div>
    </PageShell>
  );
}

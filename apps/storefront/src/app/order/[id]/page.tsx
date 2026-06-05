import type { Metadata } from "next";
import { CheckCircle2 } from "lucide-react";

import { PageShell } from "@/components/layout/page-shell";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";

export async function generateMetadata({
  params,
}: PageProps<"/order/[id]">): Promise<Metadata> {
  const { id } = await params;
  return { title: `Order ${id}` };
}

export default async function OrderPage({ params }: PageProps<"/order/[id]">) {
  const { id } = await params;

  return (
    <PageShell
      title="Order confirmed"
      description="Reflects the chosen delivery type and a tracking-style status. Reads GET /orders/{id} in Wave 2."
    >
      <div className="mb-6 flex items-center gap-3">
        <CheckCircle2 className="text-primary size-6" aria-hidden />
        <p className="text-sm">
          Thank you! Your order{" "}
          <span className="font-mono font-medium">#{id}</span> has been placed.
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Items</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {Array.from({ length: 2 }).map((_, i) => (
              <div key={i} className="flex items-center gap-4">
                <Skeleton className="size-16 rounded-md" />
                <div className="flex-1 space-y-2">
                  <Skeleton className="h-4 w-1/2" />
                  <Skeleton className="h-3 w-1/4" />
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card className="h-fit">
          <CardHeader>
            <CardTitle className="flex items-center justify-between text-base">
              Delivery
              <Badge variant="secondary">Pending</Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-4 w-1/2" />
          </CardContent>
        </Card>
      </div>
    </PageShell>
  );
}

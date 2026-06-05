import { Badge } from "@/components/ui/badge";
import type { OrderStatus } from "@/lib/bff/schemas";

const VARIANT: Record<
  OrderStatus,
  { label: string; variant: "default" | "secondary" | "destructive" | "outline" }
> = {
  created: { label: "Created", variant: "secondary" },
  authorized: { label: "Authorized", variant: "secondary" },
  paid: { label: "Paid", variant: "default" },
  fulfilling: { label: "Fulfilling", variant: "default" },
  shipped: { label: "Shipped", variant: "default" },
  delivered: { label: "Delivered", variant: "default" },
  cancelled: { label: "Cancelled", variant: "outline" },
  failed: { label: "Failed", variant: "destructive" },
};

export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  const { label, variant } = VARIANT[status];
  return <Badge variant={variant}>{label}</Badge>;
}

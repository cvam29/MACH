import Link from "next/link";

import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { RemoteImage } from "@/components/product/remote-image";
import { formatMoney } from "@/lib/format";
import type { Money } from "@/lib/bff/schemas";

export interface ProductCardData {
  slug: string;
  name: string;
  imageUrl?: string | null;
  price?: Money | null;
  /** Optional editorial/category tag — the "composability punchline". */
  tag?: string | null;
}

/**
 * The shared product card. One card surfaces commercetools price (`price`),
 * Algolia/commerce discoverability (it links to the PDP) and a Contentstack-ish
 * editorial `tag` simultaneously — the visual proof of composability.
 */
export function ProductCard({ product }: { product: ProductCardData }) {
  return (
    <Card className="group gap-0 overflow-hidden py-0 transition-shadow hover:shadow-md">
      <Link
        href={`/product/${encodeURIComponent(product.slug)}`}
        className="focus-visible:ring-ring block focus-visible:ring-2 focus-visible:outline-none"
      >
        <RemoteImage
          src={product.imageUrl}
          alt={product.name}
          className="aspect-square w-full"
          sizes="(max-width: 640px) 50vw, (max-width: 1024px) 33vw, 25vw"
        />
        <CardContent className="space-y-1 p-3">
          {product.tag ? (
            <Badge variant="secondary" className="mb-1">
              {product.tag}
            </Badge>
          ) : null}
          <p className="line-clamp-2 text-sm font-medium group-hover:underline">
            {product.name}
          </p>
          <p className="text-muted-foreground text-sm tabular-nums">
            {product.price ? formatMoney(product.price) : "—"}
          </p>
        </CardContent>
      </Link>
    </Card>
  );
}

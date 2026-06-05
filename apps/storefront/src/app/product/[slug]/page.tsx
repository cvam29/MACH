import type { Metadata } from "next";
import Link from "next/link";

import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Breadcrumbs } from "@/components/layout/breadcrumbs";
import { ProductGallery } from "@/components/product/product-gallery";
import { ProductBuyBox } from "@/components/product/product-buy-box";
import { getProductBySlug } from "@/lib/server/catalog";

export async function generateMetadata({
  params,
}: PageProps<"/product/[slug]">): Promise<Metadata> {
  const { slug } = await params;
  const product = await getProductBySlug(slug);

  if (!product) {
    return { title: titleCase(slug) };
  }

  const description =
    product.marketing?.headline ??
    product.description ??
    `${product.name} — available now at MACH Store.`;

  return {
    title: product.name,
    description,
    openGraph: {
      title: product.name,
      description,
      images: product.images.length > 0 ? [product.images[0]] : undefined,
      type: "website",
    },
  };
}

export default async function ProductPage({
  params,
}: PageProps<"/product/[slug]">) {
  const { slug } = await params;
  const product = await getProductBySlug(slug);

  if (!product) {
    return <ProductUnavailable slug={slug} />;
  }

  const firstCategory = "Catalog";

  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <Breadcrumbs
        items={[
          { label: "Home", href: "/" },
          { label: firstCategory, href: "/search" },
          { label: product.name },
        ]}
      />

      <div className="grid gap-10 lg:grid-cols-2">
        <ProductGallery images={product.images} alt={product.name} />

        <div>
          <Badge variant="secondary" className="mb-3">
            Product
          </Badge>
          <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">
            {product.name}
          </h1>

          {product.description && (
            <p className="text-muted-foreground mt-3 text-sm leading-relaxed">
              {product.description}
            </p>
          )}

          <div className="mt-8">
            <ProductBuyBox product={product} />
          </div>

          {/* Contentstack-authored marketing block, merged server-side by the
              BFF — the editorial half of the composability story. */}
          {(product.marketing?.headline || product.marketing?.body) && (
            <Card className="mt-8">
              <CardContent className="space-y-2 py-4">
                <p className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
                  Editorial · Contentstack
                </p>
                {product.marketing?.headline && (
                  <p className="font-medium">{product.marketing.headline}</p>
                )}
                {product.marketing?.body && (
                  <p className="text-muted-foreground text-sm leading-relaxed">
                    {product.marketing.body}
                  </p>
                )}
              </CardContent>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}

/**
 * Shown when the product can't be loaded — either it doesn't exist or the BFF is
 * offline. A friendly state keeps the route from hard-failing in either case.
 */
function ProductUnavailable({ slug }: { slug: string }) {
  return (
    <div className="mx-auto w-full max-w-2xl px-4 py-20 text-center sm:px-6">
      <h1 className="text-2xl font-semibold tracking-tight">
        {titleCase(slug)}
      </h1>
      <p className="text-muted-foreground mt-3 text-sm">
        This product is currently unavailable. It may not exist, or the catalog
        service (commercetools via the BFF) is offline.
      </p>
      <div className="mt-6 flex justify-center gap-3">
        <Button asChild>
          <Link href="/search">Browse products</Link>
        </Button>
        <Button asChild variant="outline">
          <Link href="/">Back home</Link>
        </Button>
      </div>
    </div>
  );
}

function titleCase(slug: string): string {
  return slug
    .split("-")
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(" ");
}

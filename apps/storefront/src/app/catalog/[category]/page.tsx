import type { Metadata } from "next";
import Link from "next/link";

import { PageShell } from "@/components/layout/page-shell";
import { ProductCard } from "@/components/product/product-card";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { getCategoryProducts } from "@/lib/server/catalog";

export async function generateMetadata({
  params,
}: PageProps<"/catalog/[category]">): Promise<Metadata> {
  const { category } = await params;
  return { title: titleCase(category) };
}

export default async function CategoryPage({
  params,
}: PageProps<"/catalog/[category]">) {
  const { category } = await params;
  const { products, nbHits } = await getCategoryProducts(category);

  return (
    <PageShell
      title={titleCase(category)}
      description={
        nbHits > 0
          ? `${nbHits} product${nbHits === 1 ? "" : "s"} from commercetools, via the BFF.`
          : "Category listing from commercetools, via the BFF."
      }
    >
      {products.length > 0 ? (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
          {products.map((product) => (
            <ProductCard key={product.slug} product={product} />
          ))}
        </div>
      ) : (
        <Card className="mx-auto max-w-xl">
          <CardContent className="space-y-3 py-10 text-center">
            <p className="text-sm font-medium">
              No products in this category yet.
            </p>
            <p className="text-muted-foreground text-sm">
              Connect the BFF (commercetools) or try searching the full catalog.
            </p>
            <Button asChild variant="outline" size="sm">
              <Link href="/search">Search all products</Link>
            </Button>
          </CardContent>
        </Card>
      )}
    </PageShell>
  );
}

function titleCase(slug: string): string {
  return slug
    .split("-")
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(" ");
}

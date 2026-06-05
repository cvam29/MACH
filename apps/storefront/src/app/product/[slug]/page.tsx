import type { Metadata } from "next";

import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

export async function generateMetadata({
  params,
}: PageProps<"/product/[slug]">): Promise<Metadata> {
  const { slug } = await params;
  return { title: titleCase(slug) };
}

export default async function ProductPage({
  params,
}: PageProps<"/product/[slug]">) {
  const { slug } = await params;

  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 lg:px-8">
      <div className="grid gap-10 lg:grid-cols-2">
        {/* Gallery */}
        <div className="space-y-4">
          <Skeleton className="aspect-square w-full rounded-xl" />
          <div className="grid grid-cols-4 gap-3">
            {Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="aspect-square rounded-lg" />
            ))}
          </div>
        </div>

        {/* Buy box — commercetools price/availability merged with Contentstack
            marketing copy in Wave 2. */}
        <div>
          <Badge variant="secondary" className="mb-3">
            Product
          </Badge>
          <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">
            {titleCase(slug)}
          </h1>
          <div className="mt-4 space-y-2">
            <Skeleton className="h-7 w-28" />
            <Skeleton className="h-4 w-40" />
          </div>

          <div className="mt-6 space-y-3">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-11/12" />
            <Skeleton className="h-4 w-3/4" />
          </div>

          <div className="mt-8 flex gap-3">
            <Button size="lg" className="flex-1 sm:flex-none">
              Add to cart
            </Button>
            <Button size="lg" variant="outline">
              Save
            </Button>
          </div>

          <Card className="mt-8">
            <CardContent className="space-y-2 py-4">
              <p className="text-sm font-medium">Editorial (Contentstack)</p>
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-5/6" />
            </CardContent>
          </Card>
        </div>
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

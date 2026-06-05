import type { Metadata } from "next";

import { PageShell } from "@/components/layout/page-shell";
import { ProductGridSkeleton } from "@/components/layout/product-grid-skeleton";

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

  return (
    <PageShell
      title={titleCase(category)}
      description="Category listing — commercetools products filtered by category land here in Wave 2."
    >
      <ProductGridSkeleton count={12} />
    </PageShell>
  );
}

function titleCase(slug: string): string {
  return slug
    .split("-")
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(" ");
}

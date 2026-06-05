import type { Metadata } from "next";

import { PageShell } from "@/components/layout/page-shell";
import { ProductGridSkeleton } from "@/components/layout/product-grid-skeleton";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";

export const metadata: Metadata = { title: "Search" };

export default async function SearchPage({
  searchParams,
}: PageProps<"/search">) {
  const params = await searchParams;
  const q = typeof params.q === "string" ? params.q : "";

  return (
    <PageShell
      title="Search"
      description="Algolia InstantSearch runs browser-side with a search-only key (Wave 2). This shell shows the layout and faceting frame."
    >
      <div className="mb-8 max-w-xl">
        <Input
          type="search"
          name="q"
          defaultValue={q}
          placeholder="Search products…"
          aria-label="Search products"
        />
      </div>

      <div className="grid gap-8 lg:grid-cols-[16rem_1fr]">
        <aside aria-label="Filters" className="space-y-4">
          <Skeleton className="h-5 w-24" />
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-4 w-full" />
          ))}
        </aside>
        <ProductGridSkeleton count={9} />
      </div>
    </PageShell>
  );
}

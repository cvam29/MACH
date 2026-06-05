import type { Metadata } from "next";
import Link from "next/link";

import { PageShell } from "@/components/layout/page-shell";
import { SearchExperience } from "@/components/search/search-experience";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { env } from "@/lib/env";

export const metadata: Metadata = { title: "Search" };

export default async function SearchPage({
  searchParams,
}: PageProps<"/search">) {
  const params = await searchParams;
  const initialQuery = typeof params.q === "string" ? params.q : "";

  // Algolia search is browser-side with a search-only public key. If the public
  // env vars are not configured, degrade gracefully with a notice rather than
  // mounting an unconfigured client.
  const configured =
    env.algolia.appId.length > 0 && env.algolia.searchKey.length > 0;

  return (
    <PageShell
      title="Search"
      description="Faceted search powered by Algolia InstantSearch, running browser-side with a search-only key."
    >
      {configured ? (
        <SearchExperience initialQuery={initialQuery} />
      ) : (
        <SearchNotConfigured />
      )}
    </PageShell>
  );
}

function SearchNotConfigured() {
  return (
    <Card className="mx-auto max-w-xl">
      <CardContent className="space-y-3 py-8 text-center">
        <p className="text-sm font-medium">Search is not configured.</p>
        <p className="text-muted-foreground text-sm">
          Set <code className="font-mono">NEXT_PUBLIC_ALGOLIA_APP_ID</code> and{" "}
          <code className="font-mono">NEXT_PUBLIC_ALGOLIA_SEARCH_KEY</code> (a
          search-only key) to enable Algolia InstantSearch.
        </p>
        <Button asChild variant="outline" size="sm">
          <Link href="/">Back home</Link>
        </Button>
      </CardContent>
    </Card>
  );
}

"use client";

import * as React from "react";
import { liteClient as algoliasearch } from "algoliasearch/lite";
import {
  Configure,
  Hits,
  InstantSearch,
  Pagination,
  RangeInput,
  RefinementList,
  SearchBox,
  Stats,
  useInstantSearch,
} from "react-instantsearch";

import { env } from "@/lib/env";
import { ProductHit } from "@/components/search/product-hit";
import { Card, CardContent } from "@/components/ui/card";

/**
 * Algolia InstantSearch experience (browser-side, search-only public key — the
 * canonical Algolia pattern; the BFF/indexer owns the admin key). Renders a
 * SearchBox, faceted RefinementLists (brand / category / price), Hits as
 * product cards, Stats ("N results in M ms") and Pagination.
 *
 * The parent server component only mounts this when the public env vars are
 * present, so `appId`/`searchKey` are guaranteed non-empty here.
 */
export function SearchExperience({ initialQuery }: { initialQuery: string }) {
  // Memoize the client so it isn't recreated on every render.
  const searchClient = React.useMemo(
    () => algoliasearch(env.algolia.appId, env.algolia.searchKey),
    []
  );

  return (
    <InstantSearch
      searchClient={searchClient}
      indexName={env.algolia.indexName}
      initialUiState={{
        [env.algolia.indexName]: { query: initialQuery },
      }}
      future={{ preserveSharedStateOnUnmount: true }}
    >
      <Configure hitsPerPage={12} />

      <div className="mb-6 max-w-xl">
        <SearchBox
          placeholder="Search products…"
          autoFocus
          classNames={{
            root: "relative",
            form: "relative",
            input:
              "border-input bg-background ring-offset-background placeholder:text-muted-foreground focus-visible:ring-ring h-10 w-full rounded-md border px-3 py-2 text-sm focus-visible:ring-2 focus-visible:outline-none",
            submit: "absolute right-2 top-1/2 -translate-y-1/2 text-muted-foreground",
            reset: "hidden",
            submitIcon: "size-4 fill-current",
          }}
        />
      </div>

      <div className="grid gap-8 lg:grid-cols-[16rem_1fr]">
        <aside aria-label="Filters" className="space-y-6">
          <Facet title="Category" attribute="categories" />
          <Facet title="Brand" attribute="brand" />
          <div className="space-y-2">
            <h3 className="text-sm font-semibold">Price</h3>
            <RangeInput
              attribute="price"
              classNames={{
                form: "flex items-center gap-2",
                input:
                  "border-input bg-background h-8 w-20 rounded-md border px-2 text-sm",
                separator: "text-muted-foreground text-xs",
                submit:
                  "bg-secondary text-secondary-foreground hover:bg-secondary/80 h-8 rounded-md px-3 text-sm",
              }}
            />
          </div>
        </aside>

        <div className="space-y-6">
          <Stats
            classNames={{ root: "text-muted-foreground text-sm" }}
          />

          <ResultsArea />

          <div className="flex justify-center pt-4">
            <Pagination
              classNames={{
                list: "flex items-center gap-1",
                item: "inline-flex",
                link: "border-input hover:bg-accent inline-flex size-9 items-center justify-center rounded-md border text-sm",
                selectedItem: "[&_a]:bg-primary [&_a]:text-primary-foreground",
                disabledItem: "opacity-50 [&_a]:pointer-events-none",
              }}
            />
          </div>
        </div>
      </div>
    </InstantSearch>
  );
}

function Facet({ title, attribute }: { title: string; attribute: string }) {
  return (
    <div className="space-y-2">
      <h3 className="text-sm font-semibold">{title}</h3>
      <RefinementList
        attribute={attribute}
        limit={6}
        showMore
        classNames={{
          list: "space-y-1.5",
          label: "flex items-center gap-2 text-sm cursor-pointer",
          checkbox:
            "size-4 rounded border-input accent-primary",
          count:
            "bg-muted text-muted-foreground ml-auto rounded px-1.5 py-0.5 text-xs tabular-nums",
          showMore:
            "text-primary mt-1 text-xs underline-offset-2 hover:underline",
          noRefinementRoot: "text-muted-foreground text-xs",
        }}
      />
    </div>
  );
}

/** Hits with an empty state when a query returns nothing. */
function ResultsArea() {
  const { results, status } = useInstantSearch();
  const isEmpty = status === "idle" && results.nbHits === 0;

  if (isEmpty) {
    return (
      <Card>
        <CardContent className="py-10 text-center">
          <p className="text-sm font-medium">No products matched your search.</p>
          <p className="text-muted-foreground mt-1 text-sm">
            Try a different term or clear the filters.
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Hits
      hitComponent={ProductHit}
      classNames={{
        list: "grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4",
        item: "h-full",
      }}
    />
  );
}

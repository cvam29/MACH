import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

/**
 * Placeholder product grid shown until Wave 2 wires real data. Accessible:
 * announced as a busy region while "loading".
 */
export function ProductGridSkeleton({ count = 8 }: { count?: number }) {
  return (
    <div
      aria-busy
      aria-label="Loading products"
      className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4"
    >
      {Array.from({ length: count }).map((_, i) => (
        <Card key={i} className="gap-3 py-0">
          <CardHeader className="px-0">
            <Skeleton className="aspect-square w-full rounded-t-xl" />
          </CardHeader>
          <CardContent className="space-y-2 pb-4">
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-4 w-1/3" />
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

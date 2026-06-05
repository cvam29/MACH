import * as React from "react";

import { cn } from "@/lib/utils";

/**
 * Consistent page container + titled header used by the placeholder routes.
 */
export function PageShell({
  title,
  description,
  className,
  children,
}: {
  title: string;
  description?: string;
  className?: string;
  children?: React.ReactNode;
}) {
  return (
    <div
      className={cn(
        "mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 lg:px-8",
        className
      )}
    >
      <header className="mb-8">
        <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">
          {title}
        </h1>
        {description && (
          <p className="text-muted-foreground mt-2 max-w-2xl text-sm">
            {description}
          </p>
        )}
      </header>
      {children}
    </div>
  );
}

"use client";

import * as React from "react";
import Image from "next/image";
import { ImageOff } from "lucide-react";

import { cn } from "@/lib/utils";

/**
 * Resilient product image. Wraps `next/image` (fill mode) and degrades to a
 * neutral placeholder when the source is missing or fails to load — so a broken
 * CDN URL or an offline backend never leaves an empty box or throws.
 */
export function RemoteImage({
  src,
  alt,
  sizes,
  className,
  priority,
}: {
  src?: string | null;
  alt: string;
  sizes?: string;
  className?: string;
  priority?: boolean;
}) {
  const [failed, setFailed] = React.useState(false);
  const showPlaceholder = !src || failed;

  return (
    <div
      className={cn(
        "bg-muted relative flex items-center justify-center overflow-hidden",
        className
      )}
    >
      {showPlaceholder ? (
        <ImageOff
          className="text-muted-foreground/40 size-8"
          aria-hidden
        />
      ) : (
        <Image
          src={src}
          alt={alt}
          fill
          sizes={sizes ?? "(max-width: 768px) 50vw, 25vw"}
          priority={priority}
          className="object-cover"
          onError={() => setFailed(true)}
          unoptimized
        />
      )}
    </div>
  );
}

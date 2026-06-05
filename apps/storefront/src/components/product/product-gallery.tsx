"use client";

import * as React from "react";

import { RemoteImage } from "@/components/product/remote-image";
import { cn } from "@/lib/utils";

/** PDP gallery: a main image with selectable thumbnails. */
export function ProductGallery({
  images,
  alt,
}: {
  images: string[];
  alt: string;
}) {
  const [active, setActive] = React.useState(0);
  const main = images[active];

  return (
    <div className="space-y-4">
      <RemoteImage
        src={main}
        alt={alt}
        className="aspect-square w-full rounded-xl border"
        sizes="(max-width: 1024px) 100vw, 50vw"
        priority
      />
      {images.length > 1 && (
        <div className="grid grid-cols-4 gap-3">
          {images.slice(0, 8).map((img, i) => (
            <button
              key={`${img}-${i}`}
              type="button"
              onClick={() => setActive(i)}
              aria-label={`View image ${i + 1}`}
              aria-pressed={i === active}
              className={cn(
                "overflow-hidden rounded-lg border transition-colors",
                i === active
                  ? "border-primary ring-primary ring-1"
                  : "border-input hover:border-foreground/40"
              )}
            >
              <RemoteImage
                src={img}
                alt=""
                className="aspect-square w-full"
                sizes="120px"
              />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

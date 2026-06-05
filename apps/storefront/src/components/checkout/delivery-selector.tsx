"use client";

import * as React from "react";
import { Clock, Loader2, MapPin } from "lucide-react";

import { cn } from "@/lib/utils";
import { formatMoney } from "@/lib/format";
import { bff } from "@/lib/bff/client";
import type {
  AddressInput,
} from "@/lib/bff/client";
import type {
  DeliveryOption,
  DeliveryType,
  Store,
} from "@/lib/bff/schemas";

/**
 * Delivery-type selector. Calls the BFF `delivery-options` route with the
 * shipping address; the BFF geocodes via Azure Maps and re-prices each type by
 * distance. Shows price + ETA per type, disables unavailable types (e.g.
 * same-day beyond the threshold), and lists nearby stores for store-pickup.
 */
export function DeliverySelector({
  cartId,
  address,
  selected,
  onSelect,
}: {
  cartId: string;
  address: AddressInput;
  selected: DeliveryType | null;
  onSelect: (type: DeliveryType, option: DeliveryOption) => void | Promise<void>;
}) {
  const [options, setOptions] = React.useState<DeliveryOption[] | null>(null);
  const [stores, setStores] = React.useState<Store[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  // Re-quote whenever the address changes (live distance-based pricing).
  const addressKey = JSON.stringify(address);
  React.useEffect(() => {
    let cancelled = false;

    (async () => {
      setLoading(true);
      setError(null);
      try {
        const result = await bff.getDeliveryOptions(cartId, address);
        if (cancelled) return;
        setOptions(result.options);

        // Pre-fetch nearby stores so store-pickup can list them.
        const near = `${address.postalCode} ${address.country}`.trim();
        try {
          const storeResult = await bff.getStores(near);
          if (!cancelled) setStores(storeResult.stores);
        } catch {
          if (!cancelled) setStores([]);
        }
      } catch {
        if (!cancelled) {
          setError(
            "Could not load delivery options. The delivery service (Azure Maps via the BFF) may be offline."
          );
          setOptions([]);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [cartId, addressKey]);

  if (loading) {
    return (
      <div className="text-muted-foreground flex items-center gap-2 py-4 text-sm">
        <Loader2 className="size-4 animate-spin" /> Pricing delivery by
        distance…
      </div>
    );
  }

  if (error) {
    return (
      <p role="alert" className="text-destructive py-2 text-sm">
        {error}
      </p>
    );
  }

  if (!options || options.length === 0) {
    return (
      <p className="text-muted-foreground py-2 text-sm">
        No delivery options available for this address.
      </p>
    );
  }

  return (
    <ul className="space-y-3" role="radiogroup" aria-label="Delivery options">
      {options.map((option) => {
        const isSelected = option.type === selected;
        const disabled = !option.available;
        return (
          <li key={option.type}>
            <button
              type="button"
              role="radio"
              aria-checked={isSelected}
              disabled={disabled}
              onClick={() => onSelect(option.type, option)}
              className={cn(
                "w-full rounded-lg border p-4 text-left transition-colors",
                "disabled:cursor-not-allowed disabled:opacity-50",
                isSelected
                  ? "border-primary ring-primary bg-primary/5 ring-1"
                  : "border-input hover:bg-accent"
              )}
            >
              <div className="flex items-start justify-between gap-3">
                <div className="space-y-1">
                  <p className="text-sm font-medium">{option.label}</p>
                  <p className="text-muted-foreground flex items-center gap-1 text-xs">
                    <Clock className="size-3.5" aria-hidden />
                    {option.eta}
                    {typeof option.distanceKm === "number" && (
                      <span className="ml-1">
                        · {option.distanceKm.toFixed(1)} km
                      </span>
                    )}
                  </p>
                  {disabled && (
                    <p className="text-muted-foreground text-xs">
                      Unavailable for this distance
                    </p>
                  )}
                </div>
                <span className="text-sm font-semibold tabular-nums">
                  {option.price.centAmount === 0
                    ? "Free"
                    : formatMoney(option.price)}
                </span>
              </div>

              {/* Store-pickup: list nearby stores. */}
              {option.type === "store-pickup" && isSelected && (
                <div className="mt-3 space-y-2 border-t pt-3">
                  {stores.length > 0 ? (
                    stores.map((store) => (
                      <div
                        key={store.id}
                        className="flex items-start gap-2 text-xs"
                      >
                        <MapPin
                          className="text-muted-foreground mt-0.5 size-3.5 shrink-0"
                          aria-hidden
                        />
                        <div>
                          <p className="font-medium">{store.name}</p>
                          {store.address && (
                            <p className="text-muted-foreground">
                              {store.address}
                            </p>
                          )}
                          <p className="text-muted-foreground">
                            {[
                              typeof store.distanceKm === "number"
                                ? `${store.distanceKm.toFixed(1)} km away`
                                : null,
                              store.eta,
                            ]
                              .filter(Boolean)
                              .join(" · ")}
                          </p>
                        </div>
                      </div>
                    ))
                  ) : (
                    <p className="text-muted-foreground text-xs">
                      Nearest store will be assigned at pickup.
                    </p>
                  )}
                </div>
              )}
            </button>
          </li>
        );
      })}
    </ul>
  );
}

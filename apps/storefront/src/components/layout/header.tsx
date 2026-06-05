import Link from "next/link";
import { Search } from "lucide-react";

import { Button } from "@/components/ui/button";
import { MiniCart } from "@/components/layout/mini-cart";
import { AccountMenu } from "@/components/layout/account-menu";

/**
 * Global header: brand, primary nav (placeholder — Contentstack-driven in
 * Wave 2), search entry point, mini-cart and account menu.
 */
const NAV_LINKS: { label: string; href: string }[] = [
  { label: "New in", href: "/catalog/new" },
  { label: "Men", href: "/catalog/men" },
  { label: "Women", href: "/catalog/women" },
  { label: "Accessories", href: "/catalog/accessories" },
];

export function Header() {
  return (
    <header className="bg-background/95 supports-[backdrop-filter]:bg-background/60 sticky top-0 z-40 border-b backdrop-blur">
      <div className="mx-auto flex h-16 max-w-7xl items-center gap-4 px-4 sm:px-6 lg:px-8">
        <Link href="/" className="flex items-center gap-2 font-semibold">
          <span className="bg-primary text-primary-foreground inline-flex size-7 items-center justify-center rounded-md text-sm font-bold">
            M
          </span>
          <span className="hidden sm:inline">MACH Store</span>
        </Link>

        <nav
          aria-label="Primary"
          className="hidden items-center gap-1 md:flex"
        >
          {NAV_LINKS.map((link) => (
            <Button key={link.href} asChild variant="ghost" size="sm">
              <Link href={link.href}>{link.label}</Link>
            </Button>
          ))}
        </nav>

        <div className="ml-auto flex items-center gap-1">
          <Button asChild variant="ghost" size="icon" aria-label="Search">
            <Link href="/search">
              <Search className="size-5" />
            </Link>
          </Button>
          <MiniCart />
          <AccountMenu />
        </div>
      </div>
    </header>
  );
}

import Link from "next/link";

/**
 * Global footer. Columns are placeholders today; Wave 2 sources them from the
 * Contentstack "Footer" content type via the BFF.
 */
const FOOTER_COLUMNS: { heading: string; links: { label: string; href: string }[] }[] =
  [
    {
      heading: "Shop",
      links: [
        { label: "New in", href: "/catalog/new" },
        { label: "Men", href: "/catalog/men" },
        { label: "Women", href: "/catalog/women" },
      ],
    },
    {
      heading: "Help",
      links: [
        { label: "Search", href: "/search" },
        { label: "Your cart", href: "/cart" },
        { label: "Account", href: "/account" },
      ],
    },
    {
      heading: "Company",
      links: [
        { label: "About", href: "/" },
        { label: "Architecture", href: "/" },
      ],
    },
  ];

export function Footer() {
  return (
    <footer className="border-t">
      <div className="mx-auto grid max-w-7xl gap-8 px-4 py-12 sm:grid-cols-2 sm:px-6 md:grid-cols-4 lg:px-8">
        <div>
          <Link href="/" className="flex items-center gap-2 font-semibold">
            <span className="bg-primary text-primary-foreground inline-flex size-7 items-center justify-center rounded-md text-sm font-bold">
              M
            </span>
            MACH Store
          </Link>
          <p className="text-muted-foreground mt-3 text-sm">
            A composable commerce demo — commercetools, Contentstack, Algolia &
            Adyen behind a .NET BFF.
          </p>
        </div>

        {FOOTER_COLUMNS.map((col) => (
          <div key={col.heading}>
            <h2 className="text-sm font-semibold">{col.heading}</h2>
            <ul className="mt-3 space-y-2">
              {col.links.map((link) => (
                <li key={`${col.heading}-${link.label}`}>
                  <Link
                    href={link.href}
                    className="text-muted-foreground hover:text-foreground text-sm transition-colors"
                  >
                    {link.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
      <div className="border-t">
        <p className="text-muted-foreground mx-auto max-w-7xl px-4 py-6 text-xs sm:px-6 lg:px-8">
          &copy; {new Date().getFullYear()} MACH Store. Portfolio demo — not a
          real shop.
        </p>
      </div>
    </footer>
  );
}

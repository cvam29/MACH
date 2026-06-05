import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  images: {
    // Product/marketing imagery comes from commercetools + Contentstack CDNs
    // (and Algolia records mirror those URLs). The exact hosts are vendor- and
    // project-specific, so allow any https/http host for this demo. In a real
    // deployment this list would be narrowed to the known CDN hostnames.
    remotePatterns: [
      { protocol: "https", hostname: "**" },
      { protocol: "http", hostname: "**" },
    ],
  },
};

export default nextConfig;

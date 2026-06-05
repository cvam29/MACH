/**
 * Contentstack seed (run AFTER Algolia).
 *
 * Uses the Contentstack Management API (CMA) over typed fetch to:
 *   - create content types: Home Hero, Promo Tile, Navigation,
 *     PDP Marketing Block, Footer, Email Template
 *   - seed entries (PDP marketing blocks keyed by product slug; one Email
 *     Template entry per audience: customer / store / supplier / reception)
 *   - publish entries to the delivery environment
 *
 * Idempotency: content types are upserted by uid, entries by title (CMA query),
 * so re-running updates in place rather than duplicating.
 */
import { requireEnv, optionalEnv, runSeed } from '../lib/env.js';
import { loadCatalog } from '../lib/catalog.js';

/** Minimal CMA field schema shape we use (a small typed subset). */
interface CmaField {
  display_name: string;
  uid: string;
  data_type: 'text' | 'boolean' | 'number' | 'reference' | 'json';
  mandatory?: boolean;
  unique?: boolean;
  multiple?: boolean;
  field_metadata?: Record<string, unknown>;
}

interface CmaContentType {
  title: string;
  uid: string;
  schema: CmaField[];
  options?: Record<string, unknown>;
}

interface CmaEntry {
  title: string;
  url?: string;
  [key: string]: unknown;
}

const TITLE_FIELD: CmaField = {
  display_name: 'Title',
  uid: 'title',
  data_type: 'text',
  mandatory: true,
  unique: true,
  field_metadata: { _default: true },
};

const URL_FIELD: CmaField = {
  display_name: 'URL',
  uid: 'url',
  data_type: 'text',
  mandatory: false,
  field_metadata: { _default: true },
};

function textField(uid: string, displayName: string, opts: Partial<CmaField> = {}): CmaField {
  return { display_name: displayName, uid, data_type: 'text', field_metadata: {}, ...opts };
}

const CONTENT_TYPES: CmaContentType[] = [
  {
    title: 'Home Hero',
    uid: 'home_hero',
    schema: [
      TITLE_FIELD,
      URL_FIELD,
      textField('headline', 'Headline', { mandatory: true }),
      textField('subheadline', 'Subheadline'),
      textField('cta_label', 'CTA Label'),
      textField('cta_url', 'CTA URL'),
      textField('image_url', 'Image URL'),
    ],
  },
  {
    title: 'Promo Tile',
    uid: 'promo_tile',
    schema: [
      TITLE_FIELD,
      URL_FIELD,
      textField('heading', 'Heading', { mandatory: true }),
      textField('body', 'Body', { field_metadata: { multiline: true } }),
      textField('cta_label', 'CTA Label'),
      textField('cta_url', 'CTA URL'),
      textField('image_url', 'Image URL'),
    ],
  },
  {
    title: 'Navigation',
    uid: 'navigation',
    schema: [
      TITLE_FIELD,
      URL_FIELD,
      { display_name: 'Items', uid: 'items', data_type: 'json', multiple: false, field_metadata: {} },
    ],
  },
  {
    title: 'PDP Marketing Block',
    uid: 'pdp_marketing_block',
    schema: [
      TITLE_FIELD,
      URL_FIELD,
      textField('product_slug', 'Product Slug', { mandatory: true, unique: false }),
      textField('marketing_headline', 'Marketing Headline'),
      textField('marketing_copy', 'Marketing Copy', { field_metadata: { multiline: true } }),
      { display_name: 'Highlights', uid: 'highlights', data_type: 'json', field_metadata: {} },
    ],
  },
  {
    title: 'Footer',
    uid: 'footer',
    schema: [
      TITLE_FIELD,
      URL_FIELD,
      textField('copyright', 'Copyright'),
      { display_name: 'Links', uid: 'links', data_type: 'json', field_metadata: {} },
    ],
  },
  {
    title: 'Email Template',
    uid: 'email_template',
    schema: [
      TITLE_FIELD,
      URL_FIELD,
      // audience: customer / store / supplier / reception
      textField('audience', 'Audience', { mandatory: true }),
      textField('subject', 'Subject', { mandatory: true }),
      // body holds {{order}} / {{delivery}} tokens rendered at send time
      textField('body', 'Body', { mandatory: true, field_metadata: { multiline: true } }),
    ],
  },
];

type Audience = 'customer' | 'store' | 'supplier' | 'reception';
const AUDIENCES: Audience[] = ['customer', 'store', 'supplier', 'reception'];

const EMAIL_TEMPLATES: Record<Audience, { subject: string; body: string }> = {
  customer: {
    subject: 'Your order {{order.number}} is confirmed',
    body:
      'Hi {{order.customerName}},\n\nThanks for your order {{order.number}}.\n' +
      'Delivery: {{delivery.type}} — estimated {{delivery.eta}} to {{delivery.address}}.\n\n' +
      'Total: {{order.total}}.\n\nWe will let you know when it ships.',
  },
  store: {
    subject: 'New order to fulfil: {{order.number}}',
    body:
      'New order {{order.number}} assigned to {{delivery.storeName}}.\n' +
      'Fulfilment: {{delivery.type}} ({{delivery.eta}}).\n' +
      'Items:\n{{order.lineItems}}\n\nPlease prepare for {{delivery.type}}.',
  },
  supplier: {
    subject: 'Replenishment request for order {{order.number}}',
    body:
      'Replenishment/dropship request for order {{order.number}}.\n' +
      'SKUs:\n{{order.lineItems}}\n\nShip to store {{delivery.storeName}} for {{delivery.type}}.',
  },
  reception: {
    subject: 'Goods-in / pickup notice for {{order.number}}',
    body:
      'Heads-up: order {{order.number}} is inbound via {{delivery.type}}.\n' +
      'Expected {{delivery.eta}} at {{delivery.storeName}}.\n' +
      'Customer: {{order.customerName}}.',
  },
};

/** Thin typed CMA HTTP client. */
class ContentstackCma {
  constructor(
    private readonly baseUrl: string,
    private readonly apiKey: string,
    private readonly managementToken: string,
  ) {}

  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    const res = await fetch(`${this.baseUrl}${path}`, {
      method,
      headers: {
        api_key: this.apiKey,
        authorization: this.managementToken,
        'Content-Type': 'application/json',
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const text = await res.text();
    const json = text ? (JSON.parse(text) as unknown) : {};
    if (!res.ok) {
      throw new Error(`CMA ${method} ${path} -> ${res.status}: ${text}`);
    }
    return json as T;
  }

  async getContentType(uid: string): Promise<{ content_type: { uid: string } } | undefined> {
    try {
      return await this.request<{ content_type: { uid: string } }>('GET', `/v3/content_types/${uid}`);
    } catch (err) {
      if (err instanceof Error && err.message.includes('-> 422')) return undefined;
      if (err instanceof Error && err.message.includes('-> 404')) return undefined;
      throw err;
    }
  }

  async upsertContentType(ct: CmaContentType): Promise<void> {
    const existing = await this.getContentType(ct.uid);
    const payload = { content_type: { title: ct.title, uid: ct.uid, schema: ct.schema, options: ct.options ?? {} } };
    if (existing) {
      await this.request('PUT', `/v3/content_types/${ct.uid}`, payload);
      console.log(`  updated content type '${ct.uid}'`);
    } else {
      await this.request('POST', '/v3/content_types', payload);
      console.log(`  created content type '${ct.uid}'`);
    }
  }

  async findEntryUidByTitle(contentTypeUid: string, title: string): Promise<string | undefined> {
    const query = encodeURIComponent(JSON.stringify({ title }));
    const res = await this.request<{ entries: { uid: string }[] }>(
      'GET',
      `/v3/content_types/${contentTypeUid}/entries?query=${query}&limit=1`,
    );
    return res.entries[0]?.uid;
  }

  async upsertEntry(contentTypeUid: string, entry: CmaEntry): Promise<string> {
    const existingUid = await this.findEntryUidByTitle(contentTypeUid, entry.title);
    if (existingUid) {
      await this.request('PUT', `/v3/content_types/${contentTypeUid}/entries/${existingUid}`, { entry });
      return existingUid;
    }
    const created = await this.request<{ entry: { uid: string } }>(
      'POST',
      `/v3/content_types/${contentTypeUid}/entries`,
      { entry },
    );
    return created.entry.uid;
  }

  async publishEntry(
    contentTypeUid: string,
    entryUid: string,
    environment: string,
    locale: string,
  ): Promise<void> {
    await this.request('POST', `/v3/content_types/${contentTypeUid}/entries/${entryUid}/publish`, {
      entry: { environments: [environment], locales: [locale] },
    });
  }
}

async function main(): Promise<void> {
  const env = requireEnv([
    'CONTENTSTACK_API_KEY',
    'CONTENTSTACK_MANAGEMENT_TOKEN',
    'CONTENTSTACK_ENVIRONMENT',
  ] as const);
  const baseUrl = optionalEnv('CONTENTSTACK_CMA_BASE_URL', 'https://api.contentstack.io');
  const locale = optionalEnv('CONTENTSTACK_LOCALE', 'en-us');

  const cma = new ContentstackCma(baseUrl, env.CONTENTSTACK_API_KEY, env.CONTENTSTACK_MANAGEMENT_TOKEN);
  const catalog = loadCatalog();

  console.log(`Seeding Contentstack (${CONTENT_TYPES.length} content types)…`);
  for (const ct of CONTENT_TYPES) {
    await cma.upsertContentType(ct);
  }

  const publish = async (contentTypeUid: string, entry: CmaEntry): Promise<void> => {
    const uid = await cma.upsertEntry(contentTypeUid, entry);
    await cma.publishEntry(contentTypeUid, uid, env.CONTENTSTACK_ENVIRONMENT, locale);
  };

  // Home Hero + a Promo Tile + Navigation + Footer (one of each).
  await publish('home_hero', {
    title: 'Spring Campaign Hero',
    url: '/home/hero',
    headline: 'Built to move with you',
    subheadline: 'Composable apparel for every day.',
    cta_label: 'Shop the collection',
    cta_url: '/catalog/tops',
    image_url: 'https://example.com/hero.jpg',
  });

  await publish('promo_tile', {
    title: 'Outerwear Promo',
    url: '/home/promo/outerwear',
    heading: 'Up to 25% off outerwear',
    body: 'Jackets and coats for any season — limited time.',
    cta_label: 'Shop outerwear',
    cta_url: '/catalog/outerwear',
    image_url: 'https://example.com/promo-outerwear.jpg',
  });

  await publish('navigation', {
    title: 'Main Navigation',
    url: '/navigation/main',
    items: catalog.categories.map((c) => ({ label: c.name, url: `/catalog/${c.slug}` })),
  });

  await publish('footer', {
    title: 'Site Footer',
    url: '/footer',
    copyright: `© ${new Date().getFullYear()} MACH Demo. All rights reserved.`,
    links: [
      { label: 'About', url: '/about' },
      { label: 'Contact', url: '/contact' },
      { label: 'Privacy', url: '/privacy' },
    ],
  });

  // PDP marketing block per product slug.
  console.log(`  seeding ${catalog.products.length} PDP marketing blocks…`);
  for (const product of catalog.products) {
    await publish('pdp_marketing_block', {
      title: `PDP — ${product.name} (${product.slug})`,
      url: `/pdp/${product.slug}`,
      product_slug: product.slug,
      marketing_headline: `Meet the ${product.name}`,
      marketing_copy: product.description,
      highlights: [product.material, product.brand, `Colour: ${product.color}`],
    });
  }

  // One Email Template entry per audience.
  for (const audience of AUDIENCES) {
    const tpl = EMAIL_TEMPLATES[audience];
    await publish('email_template', {
      title: `Email Template — ${audience}`,
      url: `/email/${audience}`,
      audience,
      subject: tpl.subject,
      body: tpl.body,
    });
  }
  console.log(`  seeded ${AUDIENCES.length} email templates (one per audience)`);
}

await runSeed('contentstack', main);

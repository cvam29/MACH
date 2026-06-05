/**
 * Replay a sample Adyen payment notification at the Webhooks host — so the event-driven chain
 * (Webhooks -> Service Bus -> Projection -> Notifications) fires fully offline, with no real Adyen.
 *
 * It signs the notification with the SAME HMAC scheme the Adyen .NET SDK verifies on the server
 * (Adyen webhook HMAC: an escaped, ':'-joined signing string, HMAC-SHA256 with the hex-decoded key,
 * base64-encoded). As long as ADYEN_HMAC_KEY here matches `Adyen:HmacKey` configured on the Webhooks
 * host, the signature validates.
 *
 * Usage (from seed/):
 *   ADYEN_HMAC_KEY=<hexKey> npm run replay:adyen -- --order <orderId> [--success false] [--post]
 *
 * Without --post it prints the signed payload (dry run). With --post it sends it to WEBHOOK_URL
 * (default http://localhost:7072/api/hooks/adyen). The orderId becomes the notification's
 * merchantReference, which the Projection host uses as the commercetools order id.
 *
 * Generate a matching dev key:  node -e "console.log(require('crypto').randomBytes(32).toString('hex').toUpperCase())"
 */
import { createHmac } from 'node:crypto';

const args = process.argv.slice(2);
const flag = (name: string): string | undefined => {
  const i = args.indexOf(`--${name}`);
  return i >= 0 && i + 1 < args.length && !args[i + 1].startsWith('--') ? args[i + 1] : undefined;
};
const has = (name: string): boolean => args.includes(`--${name}`);

const hmacKeyHex = process.env.ADYEN_HMAC_KEY;
const webhookUrl = process.env.WEBHOOK_URL ?? 'http://localhost:7072/api/hooks/adyen';
const orderId = flag('order') ?? 'demo-order-1';
const success = (flag('success') ?? 'true').toLowerCase() !== 'false';
const post = has('post');

if (!hmacKeyHex) {
  console.error('Missing ADYEN_HMAC_KEY (hex). It must match Adyen:HmacKey on the Webhooks host.');
  process.exit(1);
}

// Adyen webhook HMAC: escape '\' and ':' in each field, join with ':' in this exact order,
// HMAC-SHA256 with the hex-decoded key, base64-encode.
const escape = (v: string): string => v.replace(/\\/g, '\\\\').replace(/:/g, '\\:');

const item = {
  pspReference: `TESTPSP${Date.now()}`,
  originalReference: '',
  merchantAccountCode: 'MachDemoMerchant',
  merchantReference: orderId,
  amount: { value: 9900, currency: 'EUR' },
  eventCode: 'AUTHORISATION',
  success: success ? 'true' : 'false',
};

const signingString = [
  item.pspReference,
  item.originalReference,
  item.merchantAccountCode,
  item.merchantReference,
  String(item.amount.value),
  item.amount.currency,
  item.eventCode,
  item.success,
]
  .map(escape)
  .join(':');

const hmacSignature = createHmac('sha256', Buffer.from(hmacKeyHex, 'hex'))
  .update(signingString, 'utf8')
  .digest('base64');

const notification = {
  live: 'false',
  notificationItems: [
    {
      NotificationRequestItem: {
        ...item,
        additionalData: { hmacSignature },
      },
    },
  ],
};

const body = JSON.stringify(notification, null, 2);

if (!post) {
  console.log('# Dry run — pass --post to send to', webhookUrl);
  console.log('# Adyen-Hmac-Signature:', hmacSignature);
  console.log(body);
  process.exit(0);
}

const res = await fetch(webhookUrl, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', 'Adyen-Hmac-Signature': hmacSignature },
  body,
});
const text = await res.text();
console.log(`POST ${webhookUrl} -> ${res.status} ${text}`);
if (text.trim() !== '[accepted]') {
  console.error('Expected [accepted]; check that ADYEN_HMAC_KEY matches the Webhooks host config.');
  process.exit(1);
}

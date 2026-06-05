/**
 * Shared environment loading + validation helpers for all seed scripts.
 *
 * Loads variables from `../.env` (the repo root) and the process environment.
 * Each seed script declares the env vars it needs and gets a clear, friendly
 * error (and a non-zero exit) when any are missing — so the scripts type-check
 * and run safely even without real sandbox credentials.
 */
import { config as loadDotenv } from 'dotenv';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
// seed/lib -> seed -> MACH root, where the shared .env lives.
const repoRootEnv = resolve(here, '..', '..', '.env');
const seedEnv = resolve(here, '..', '.env');

// Load the repo-root .env first, then a seed-local .env (override) if present.
// `override: false` keeps already-set process env vars authoritative.
loadDotenv({ path: repoRootEnv, override: false });
loadDotenv({ path: seedEnv, override: false });

export class MissingEnvError extends Error {
  constructor(missing: string[]) {
    super(
      `Missing required environment variable(s): ${missing.join(', ')}.\n` +
        `Set them in '${repoRootEnv}' (preferred) or '${seedEnv}', or export them in your shell.\n` +
        `See seed/README.md for the full list and seed/.env.example for a template.`,
    );
    this.name = 'MissingEnvError';
  }
}

/**
 * Returns the requested env vars, throwing a single aggregated MissingEnvError
 * listing every absent key. Use at the top of each seed script.
 */
export function requireEnv<const K extends readonly string[]>(
  keys: K,
): Record<K[number], string> {
  const missing: string[] = [];
  const out = {} as Record<K[number], string>;
  for (const key of keys) {
    const value = process.env[key];
    if (value === undefined || value.trim() === '') {
      missing.push(key);
    } else {
      out[key as K[number]] = value;
    }
  }
  if (missing.length > 0) {
    throw new MissingEnvError(missing);
  }
  return out;
}

/** Optional env var with a default. */
export function optionalEnv(key: string, fallback: string): string {
  const value = process.env[key];
  return value === undefined || value.trim() === '' ? fallback : value;
}

/**
 * Runs a seed `main` function, printing a friendly message and exiting with
 * code 1 when env is missing or any other error is thrown. Keeps each script's
 * tail boilerplate-free.
 */
export async function runSeed(name: string, main: () => Promise<void>): Promise<void> {
  const started = Date.now();
  console.log(`\n[${name}] starting…`);
  try {
    await main();
    console.log(`[${name}] done in ${((Date.now() - started) / 1000).toFixed(1)}s ✓`);
  } catch (err) {
    if (err instanceof MissingEnvError) {
      console.error(`\n[${name}] cannot run — configuration incomplete:\n${err.message}\n`);
    } else {
      console.error(`\n[${name}] failed:`, err instanceof Error ? err.stack ?? err.message : err);
    }
    process.exitCode = 1;
  }
}

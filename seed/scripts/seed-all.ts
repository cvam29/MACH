/**
 * seed:all orchestrator.
 *
 * Runs the seed scripts in the required order:
 *   1. commercetools  (catalog source of truth for commerce)
 *   2. Algolia        (flattens the same catalog into search records)
 *   3. Contentstack   (content types + entries, incl. per-audience email templates)
 *   4. SQL fulfillment(Stores / Suppliers / ProductSuppliers for delivery + notifications)
 *
 * Each child runs as its own tsx process so a friendly env-missing exit in one
 * script doesn't crash the whole run silently — we report a summary at the end
 * and exit non-zero if any step failed.
 *
 * Use SEED_CONTINUE_ON_ERROR=true to keep going past a failing step (handy when
 * only some vendors are configured); default is to stop at the first failure.
 */
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';
import { optionalEnv } from '../lib/env.js';

const here = dirname(fileURLToPath(import.meta.url));
const seedRoot = resolve(here, '..');

interface Step {
  name: string;
  script: string;
}

const STEPS: Step[] = [
  { name: 'commercetools', script: 'commercetools/seed-commercetools.ts' },
  { name: 'algolia', script: 'algolia/seed-algolia.ts' },
  { name: 'contentstack', script: 'contentstack/seed-contentstack.ts' },
  { name: 'sql-fulfillment', script: 'sql/seed-fulfillment.ts' },
];

function runStep(step: Step): Promise<number> {
  return new Promise((resolvePromise) => {
    const child = spawn(
      process.execPath,
      ['--import', 'tsx', resolve(seedRoot, step.script)],
      { cwd: seedRoot, stdio: 'inherit', env: process.env },
    );
    child.on('exit', (code) => resolvePromise(code ?? 1));
    child.on('error', (err) => {
      console.error(`[seed:all] failed to launch ${step.name}:`, err);
      resolvePromise(1);
    });
  });
}

async function main(): Promise<void> {
  const continueOnError = optionalEnv('SEED_CONTINUE_ON_ERROR', 'false') === 'true';
  const results: { name: string; code: number }[] = [];

  console.log('\n=== MACH seed:all — order: commercetools → algolia → contentstack → sql ===');
  for (const step of STEPS) {
    console.log(`\n--- running ${step.name} ---`);
    const code = await runStep(step);
    results.push({ name: step.name, code });
    if (code !== 0 && !continueOnError) {
      console.error(`\n[seed:all] '${step.name}' exited ${code}; stopping (set SEED_CONTINUE_ON_ERROR=true to continue).`);
      break;
    }
  }

  console.log('\n=== seed:all summary ===');
  for (const r of results) {
    console.log(`  ${r.code === 0 ? '✓' : '✗'} ${r.name} (exit ${r.code})`);
  }
  const skipped = STEPS.filter((s) => !results.some((r) => r.name === s.name));
  for (const s of skipped) console.log(`  – ${s.name} (skipped)`);

  const anyFailed = results.some((r) => r.code !== 0) || skipped.length > 0;
  process.exitCode = anyFailed ? 1 : 0;
}

await main();

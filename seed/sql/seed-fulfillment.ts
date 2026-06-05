/**
 * SQL fulfillment seed runner.
 *
 * Executes sql/fulfillment-seed.sql against the local SQL Server `fulfillment`
 * schema (LocalDB or a mssql container). The schema/tables are created by the
 * Persistence (EF Core) track — this runner only inserts seed rows and is fully
 * idempotent (the SQL uses MERGE upserts).
 *
 * Connection: prefers a full connection string in SQL_CONNECTION_STRING; falls
 * back to discrete SQL_SERVER / SQL_DATABASE (+ optional SQL_USER / SQL_PASSWORD,
 * else integrated/trusted auth). At least one of those must be present.
 */
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';
import sql from 'mssql';
import { optionalEnv, runSeed, MissingEnvError } from '../lib/env.js';

const here = dirname(fileURLToPath(import.meta.url));
const sqlFilePath = resolve(here, 'fulfillment-seed.sql');

function buildConfig(): sql.config | string {
  const connStr = process.env.SQL_CONNECTION_STRING;
  if (connStr && connStr.trim() !== '') {
    return connStr;
  }

  const server = process.env.SQL_SERVER;
  const database = process.env.SQL_DATABASE;
  if (!server || !database) {
    throw new MissingEnvError(['SQL_CONNECTION_STRING (or SQL_SERVER + SQL_DATABASE)']);
  }

  const user = process.env.SQL_USER;
  const password = process.env.SQL_PASSWORD;
  const encrypt = optionalEnv('SQL_ENCRYPT', 'false') === 'true';
  const trustServerCertificate = optionalEnv('SQL_TRUST_SERVER_CERTIFICATE', 'true') === 'true';

  const config: sql.config = {
    server,
    database,
    options: { encrypt, trustServerCertificate, enableArithAbort: true },
    pool: { max: 4, min: 0, idleTimeoutMillis: 30_000 },
  };

  if (user && password) {
    config.user = user;
    config.password = password;
  } else {
    // No SQL login provided — use Windows integrated/trusted auth (LocalDB default).
    config.options = { ...config.options, trustedConnection: true };
  }

  return config;
}

async function main(): Promise<void> {
  const script = readFileSync(sqlFilePath, 'utf-8');
  const config = buildConfig();

  const target = typeof config === 'string' ? '(connection string)' : `${config.server}/${config.database}`;
  console.log(`Applying fulfillment seed to ${target}…`);

  const pool = await sql.connect(config);
  try {
    // The .sql file is a single batch (no GO separators), so one request runs it all.
    const result = await pool.request().batch(script);
    if (result.recordset) {
      console.log('  result:', result.recordset);
    }
    console.log('  fulfillment seed applied (Stores, Suppliers, ProductSuppliers).');
  } finally {
    await pool.close();
  }
}

await runSeed('sql-fulfillment', main);

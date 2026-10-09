import type { SQLiteDatabase } from "expo-sqlite";
import { offlineDatabaseMigrations } from "@/src/offline/schema";

const databaseName = "rhema-mobile-pos.db";
let databaseFlight: Promise<SQLiteDatabase> | null = null;

export interface OfflineScope {
  scopeKey: string;
  tenantId: string;
  userId: string;
  deviceId: string;
  storeId: string;
  tillId: string;
}

export function createOfflineScope(input: Omit<OfflineScope, "scopeKey">): OfflineScope {
  const values = [input.tenantId, input.userId, input.deviceId, input.storeId, input.tillId]
    .map(value => value.trim().toLocaleLowerCase());
  if (values.some(value => value.length === 0 || value.length > 100)) {
    throw new Error("The Mobile POS offline scope is incomplete.");
  }
  return { ...input, scopeKey: values.join(":") };
}

export async function openOfflineDatabase(): Promise<SQLiteDatabase> {
  if (!databaseFlight) databaseFlight = initializeDatabase();
  try {
    return await databaseFlight;
  } catch (error) {
    databaseFlight = null;
    throw error;
  }
}

async function initializeDatabase(): Promise<SQLiteDatabase> {
  const SQLite = await import("expo-sqlite");
  const database = await SQLite.openDatabaseAsync(databaseName);
  await database.execAsync(`
    PRAGMA journal_mode = WAL;
    PRAGMA foreign_keys = ON;
    PRAGMA busy_timeout = 5000;
    CREATE TABLE IF NOT EXISTS schema_migration (
      version INTEGER PRIMARY KEY NOT NULL,
      name TEXT NOT NULL,
      applied_at_utc TEXT NOT NULL
    );
  `);
  const current = await database.getFirstAsync<{ version: number | null }>(
    "SELECT MAX(version) AS version FROM schema_migration",
  );
  let version = current?.version ?? 0;
  for (const migration of offlineDatabaseMigrations) {
    if (migration.version <= version) continue;
    await database.withExclusiveTransactionAsync(async transaction => {
      await transaction.execAsync(migration.sql);
      await transaction.runAsync(
        "INSERT INTO schema_migration(version, name, applied_at_utc) VALUES (?, ?, ?)",
        migration.version,
        migration.name,
        new Date().toISOString(),
      );
    });
    version = migration.version;
  }
  return database;
}

export async function activateOfflineScope(scope: OfflineScope): Promise<void> {
  const database = await openOfflineDatabase();
  await database.runAsync(
    `INSERT INTO cache_context(scope_key, tenant_id, user_id, device_id, store_id, till_id, activated_at_utc)
     VALUES (?, ?, ?, ?, ?, ?, ?)
     ON CONFLICT(scope_key) DO UPDATE SET activated_at_utc = excluded.activated_at_utc`,
    scope.scopeKey,
    scope.tenantId,
    scope.userId,
    scope.deviceId,
    scope.storeId,
    scope.tillId,
    new Date().toISOString(),
  );
}

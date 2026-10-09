export interface OfflineDatabaseMigration {
  version: number;
  name: string;
  sql: string;
}

export const OFFLINE_DATABASE_VERSION = 1;

export const offlineDatabaseMigrations: OfflineDatabaseMigration[] = [
  {
    version: 1,
    name: "mobile_pos_offline_foundation",
    sql: `
      CREATE TABLE IF NOT EXISTS cache_context (
        scope_key TEXT PRIMARY KEY NOT NULL,
        tenant_id TEXT NOT NULL,
        user_id TEXT NOT NULL,
        device_id TEXT NOT NULL,
        store_id TEXT NOT NULL,
        till_id TEXT NOT NULL,
        activated_at_utc TEXT NOT NULL
      );

      CREATE TABLE IF NOT EXISTS catalogue_item_cache (
        scope_key TEXT NOT NULL,
        inventory_item_id TEXT NOT NULL,
        item_code TEXT NOT NULL,
        name TEXT NOT NULL,
        barcode TEXT NULL,
        alternate_barcode TEXT NULL,
        qr_code TEXT NULL,
        search_text TEXT NOT NULL,
        changed_at_utc TEXT NOT NULL,
        projection_json TEXT NOT NULL,
        PRIMARY KEY (scope_key, inventory_item_id),
        FOREIGN KEY (scope_key) REFERENCES cache_context(scope_key) ON DELETE CASCADE
      );
      CREATE INDEX IF NOT EXISTS ix_catalogue_item_cache_search
        ON catalogue_item_cache(scope_key, search_text);

      CREATE TABLE IF NOT EXISTS customer_cache (
        scope_key TEXT NOT NULL,
        business_partner_role_id TEXT NOT NULL,
        search_text TEXT NOT NULL,
        changed_at_utc TEXT NOT NULL,
        projection_json TEXT NOT NULL,
        PRIMARY KEY (scope_key, business_partner_role_id),
        FOREIGN KEY (scope_key) REFERENCES cache_context(scope_key) ON DELETE CASCADE
      );

      CREATE TABLE IF NOT EXISTS payment_method_cache (
        scope_key TEXT NOT NULL,
        payment_method_id TEXT NOT NULL,
        changed_at_utc TEXT NOT NULL,
        projection_json TEXT NOT NULL,
        PRIMARY KEY (scope_key, payment_method_id),
        FOREIGN KEY (scope_key) REFERENCES cache_context(scope_key) ON DELETE CASCADE
      );

      CREATE TABLE IF NOT EXISTS receipt_cache (
        scope_key TEXT NOT NULL,
        sale_id TEXT NOT NULL,
        receipt_number TEXT NOT NULL,
        received_at_utc TEXT NOT NULL,
        projection_json TEXT NOT NULL,
        PRIMARY KEY (scope_key, sale_id),
        FOREIGN KEY (scope_key) REFERENCES cache_context(scope_key) ON DELETE CASCADE
      );

      CREATE TABLE IF NOT EXISTS sync_cursor (
        scope_key TEXT NOT NULL,
        collection_name TEXT NOT NULL,
        watermark_utc TEXT NULL,
        page_cursor TEXT NULL,
        snapshot_at_utc TEXT NULL,
        updated_at_utc TEXT NOT NULL,
        PRIMARY KEY (scope_key, collection_name),
        FOREIGN KEY (scope_key) REFERENCES cache_context(scope_key) ON DELETE CASCADE
      );

      CREATE TABLE IF NOT EXISTS outbox_message (
        scope_key TEXT NOT NULL,
        client_mutation_id TEXT NOT NULL,
        local_reference TEXT NOT NULL,
        command_type TEXT NOT NULL,
        schema_version INTEGER NOT NULL,
        tenant_id TEXT NOT NULL,
        device_id TEXT NOT NULL,
        store_id TEXT NOT NULL,
        till_id TEXT NOT NULL,
        till_session_id TEXT NOT NULL,
        offline_grant_id TEXT NOT NULL,
        payload_json TEXT NOT NULL,
        payload_hash TEXT NOT NULL,
        state TEXT NOT NULL,
        attempt_count INTEGER NOT NULL DEFAULT 0,
        created_at_utc TEXT NOT NULL,
        last_attempt_at_utc TEXT NULL,
        retry_after_utc TEXT NULL,
        server_result_json TEXT NULL,
        error_code TEXT NULL,
        error_detail TEXT NULL,
        PRIMARY KEY (scope_key, client_mutation_id),
        UNIQUE (scope_key, local_reference),
        FOREIGN KEY (scope_key) REFERENCES cache_context(scope_key) ON DELETE RESTRICT
      );
      CREATE INDEX IF NOT EXISTS ix_outbox_message_dispatch
        ON outbox_message(scope_key, state, retry_after_utc, created_at_utc);
    `,
  },
];

import type { SQLiteDatabase } from "expo-sqlite";
import { activateOfflineScope, openOfflineDatabase, type OfflineScope } from "@/src/offline/database";
import type { MobilePosCustomerChangePage, MobilePosCustomerSearchResult } from "@/src/types/api";

const collectionName = "customers";

interface CursorRow {
  watermark_utc: string | null;
  page_cursor: string | null;
}

interface ProjectionRow {
  projection_json: string;
}

export class SqliteCustomerCache {
  private constructor(private readonly database: SQLiteDatabase, readonly scope: OfflineScope) {}

  static async open(scope: OfflineScope): Promise<SqliteCustomerCache> {
    await activateOfflineScope(scope);
    return new SqliteCustomerCache(await openOfflineDatabase(), scope);
  }

  async synchronize(
    fetchPage: (watermarkUtc?: string, pageCursor?: string) => Promise<MobilePosCustomerChangePage>,
    maximumPages = 100,
  ): Promise<number> {
    let state = await this.loadState();
    let applied = 0;
    for (let pageNumber = 0; pageNumber < maximumPages; pageNumber += 1) {
      const page = await fetchPage(state.watermarkUtc, state.pageCursor);
      if (page.hasMore && !page.nextCursor) throw new Error("The customer change response did not include its continuation cursor.");
      await this.applyPage(page);
      applied += page.upserts.length + page.tombstoneBusinessPartnerRoleIds.length;
      if (!page.hasMore) return applied;
      state = { watermarkUtc: state.watermarkUtc, pageCursor: page.nextCursor };
    }
    throw new Error("Customer synchronization exceeded the safe page limit.");
  }

  async search(rawTerm: string, limit = 20): Promise<MobilePosCustomerSearchResult[]> {
    const term = normalize(rawTerm);
    const rows = await this.database.getAllAsync<ProjectionRow>(
      `SELECT projection_json
       FROM customer_cache
       WHERE scope_key = ? AND search_text LIKE ? ESCAPE '\\'
       ORDER BY business_partner_role_id
       LIMIT ?`,
      this.scope.scopeKey,
      `%${escapeLike(term)}%`,
      Math.max(1, Math.min(limit, 50)),
    );
    return rows.map(row => JSON.parse(row.projection_json) as MobilePosCustomerSearchResult);
  }

  private async loadState(): Promise<{ watermarkUtc?: string; pageCursor?: string }> {
    const row = await this.database.getFirstAsync<CursorRow>(
      "SELECT watermark_utc, page_cursor FROM sync_cursor WHERE scope_key = ? AND collection_name = ?",
      this.scope.scopeKey,
      collectionName,
    );
    return { watermarkUtc: row?.watermark_utc ?? undefined, pageCursor: row?.page_cursor ?? undefined };
  }

  private async applyPage(page: MobilePosCustomerChangePage): Promise<void> {
    await this.database.withExclusiveTransactionAsync(async transaction => {
      for (const customer of page.upserts) {
        await transaction.runAsync(
          `INSERT INTO customer_cache(
             scope_key, business_partner_role_id, search_text, changed_at_utc, projection_json)
           VALUES (?, ?, ?, ?, ?)
           ON CONFLICT(scope_key, business_partner_role_id) DO UPDATE SET
             search_text = excluded.search_text,
             changed_at_utc = excluded.changed_at_utc,
             projection_json = excluded.projection_json`,
          this.scope.scopeKey,
          customer.businessPartnerRoleId,
          normalize([customer.code, customer.name, customer.email, customer.phone].filter(Boolean).join(" ")),
          customer.changedAtUtc,
          JSON.stringify(customer),
        );
      }
      for (const roleId of page.tombstoneBusinessPartnerRoleIds) {
        await transaction.runAsync(
          "DELETE FROM customer_cache WHERE scope_key = ? AND business_partner_role_id = ?",
          this.scope.scopeKey,
          roleId,
        );
      }
      await transaction.runAsync(
        `INSERT INTO sync_cursor(scope_key, collection_name, watermark_utc, page_cursor, snapshot_at_utc, updated_at_utc)
         VALUES (?, ?, ?, ?, ?, ?)
         ON CONFLICT(scope_key, collection_name) DO UPDATE SET
           watermark_utc = excluded.watermark_utc,
           page_cursor = excluded.page_cursor,
           snapshot_at_utc = excluded.snapshot_at_utc,
           updated_at_utc = excluded.updated_at_utc`,
        this.scope.scopeKey,
        collectionName,
        page.hasMore ? null : page.snapshotAtUtc,
        page.hasMore ? page.nextCursor ?? null : null,
        page.snapshotAtUtc,
        new Date().toISOString(),
      );
    });
  }
}

function normalize(value: string): string {
  return value.trim().toLocaleLowerCase().replace(/\s+/g, " ");
}

function escapeLike(value: string): string {
  return value.replace(/[\\%_]/g, match => `\\${match}`);
}

import type { SQLiteDatabase } from "expo-sqlite";
import { activateOfflineScope, openOfflineDatabase, type OfflineScope } from "@/src/offline/database";
import type { MobilePosCatalogueChangePage, MobilePosCatalogueItem } from "@/src/types/api";

const collectionName = "catalogue";

interface CursorRow {
  watermark_utc: string | null;
  page_cursor: string | null;
}

interface ProjectionRow {
  projection_json: string;
}

export interface CatalogueSyncPort {
  loadState(): Promise<{ watermarkUtc?: string; pageCursor?: string }>;
  applyPage(page: MobilePosCatalogueChangePage): Promise<void>;
}

export async function synchronizeCatalogue(
  port: CatalogueSyncPort,
  fetchPage: (watermarkUtc?: string, pageCursor?: string) => Promise<MobilePosCatalogueChangePage>,
  maximumPages = 100,
): Promise<number> {
  let state = await port.loadState();
  let applied = 0;
  for (let pageNumber = 0; pageNumber < maximumPages; pageNumber += 1) {
    const page = await fetchPage(state.watermarkUtc, state.pageCursor);
    if (page.hasMore && !page.nextCursor) throw new Error("The catalogue change response did not include its continuation cursor.");
    await port.applyPage(page);
    applied += page.upserts.length + page.tombstoneInventoryItemIds.length;
    if (!page.hasMore) return applied;
    state = { watermarkUtc: state.watermarkUtc, pageCursor: page.nextCursor };
  }
  throw new Error("Catalogue synchronization exceeded the safe page limit.");
}

export class SqliteCatalogueCache implements CatalogueSyncPort {
  private constructor(private readonly database: SQLiteDatabase, readonly scope: OfflineScope) {}

  static async open(scope: OfflineScope): Promise<SqliteCatalogueCache> {
    await activateOfflineScope(scope);
    return new SqliteCatalogueCache(await openOfflineDatabase(), scope);
  }

  async loadState(): Promise<{ watermarkUtc?: string; pageCursor?: string }> {
    const row = await this.database.getFirstAsync<CursorRow>(
      "SELECT watermark_utc, page_cursor FROM sync_cursor WHERE scope_key = ? AND collection_name = ?",
      this.scope.scopeKey,
      collectionName,
    );
    return {
      watermarkUtc: row?.watermark_utc ?? undefined,
      pageCursor: row?.page_cursor ?? undefined,
    };
  }

  async applyPage(page: MobilePosCatalogueChangePage): Promise<void> {
    await this.database.withExclusiveTransactionAsync(async transaction => {
      for (const item of page.upserts) {
        await transaction.runAsync(
          `INSERT INTO catalogue_item_cache(
             scope_key, inventory_item_id, item_code, name, barcode, alternate_barcode, qr_code,
             search_text, changed_at_utc, projection_json)
           VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
           ON CONFLICT(scope_key, inventory_item_id) DO UPDATE SET
             item_code = excluded.item_code,
             name = excluded.name,
             barcode = excluded.barcode,
             alternate_barcode = excluded.alternate_barcode,
             qr_code = excluded.qr_code,
             search_text = excluded.search_text,
             changed_at_utc = excluded.changed_at_utc,
             projection_json = excluded.projection_json`,
          this.scope.scopeKey,
          item.inventoryItemId,
          item.itemCode,
          item.name,
          item.barcode ?? null,
          item.alternateBarcode ?? null,
          item.qrCode ?? null,
          catalogueSearchText(item),
          item.changedAtUtc,
          JSON.stringify(item),
        );
      }
      for (const inventoryItemId of page.tombstoneInventoryItemIds) {
        await transaction.runAsync(
          "DELETE FROM catalogue_item_cache WHERE scope_key = ? AND inventory_item_id = ?",
          this.scope.scopeKey,
          inventoryItemId,
        );
      }
      await transaction.runAsync(
        `INSERT INTO sync_cursor(
           scope_key, collection_name, watermark_utc, page_cursor, snapshot_at_utc, updated_at_utc)
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

  async search(rawTerm: string, limit = 30): Promise<MobilePosCatalogueItem[]> {
    const term = normalizeCatalogueSearch(rawTerm);
    const take = Math.max(1, Math.min(limit, 50));
    const rows = await this.database.getAllAsync<ProjectionRow>(
      `SELECT projection_json
       FROM catalogue_item_cache
       WHERE scope_key = ? AND search_text LIKE ? ESCAPE '\\'
       ORDER BY item_code, inventory_item_id
       LIMIT ?`,
      this.scope.scopeKey,
      `%${escapeLike(term)}%`,
      take,
    );
    return rows.map(row => JSON.parse(row.projection_json) as MobilePosCatalogueItem);
  }
}

export function normalizeCatalogueSearch(value: string): string {
  return value.trim().toLocaleLowerCase().replace(/\s+/g, " ");
}

export function catalogueSearchText(item: MobilePosCatalogueItem): string {
  return normalizeCatalogueSearch([
    item.itemCode,
    item.name,
    item.barcode,
    item.alternateBarcode,
    item.qrCode,
  ].filter(Boolean).join(" "));
}

function escapeLike(value: string): string {
  return value.replace(/[\\%_]/g, match => `\\${match}`);
}

import { describe, expect, it } from "vitest";
import { catalogueSearchText, normalizeCatalogueSearch, synchronizeCatalogue, type CatalogueSyncPort } from "@/src/offline/catalogue-cache";
import { createOfflineScope } from "@/src/offline/database";
import { OFFLINE_DATABASE_VERSION, offlineDatabaseMigrations } from "@/src/offline/schema";
import type { MobilePosCatalogueChangePage, MobilePosCatalogueItem } from "@/src/types/api";

const item: MobilePosCatalogueItem = {
  inventoryItemId: "item-1",
  itemCode: "SKU-001",
  name: "Blue Widget",
  barcode: "0123456789",
  itemType: "StockItem",
  unitOfMeasureCode: "EA",
  unitPrice: 12,
  currencyCode: "GHS",
  availableQuantity: 4,
  isAvailable: true,
  changedAtUtc: "2026-10-09T10:00:00.000Z",
};

describe("offline database foundation", () => {
  it("defines the durable scoped cache, outbox, receipt, and cursor tables", () => {
    expect(OFFLINE_DATABASE_VERSION).toBe(5);
    const sql = offlineDatabaseMigrations.map(migration => migration.sql).join("\n");
    for (const table of ["cache_context", "catalogue_item_cache", "customer_cache", "payment_method_cache", "configuration_cache", "receipt_cache", "receipt_document_cache", "sync_cursor", "outbox_message", "outstanding_invoice_cache", "outstanding_invoice_snapshot"]) {
      expect(sql).toContain(`CREATE TABLE IF NOT EXISTS ${table}`);
    }
    expect(sql).not.toMatch(/access_token|refresh_token|signing_secret/i);
    expect(sql).toContain("CREATE TRIGGER IF NOT EXISTS outbox_state_transition_guard");
    expect(sql).toContain("CHECK (receipt_kind IN ('SALE', 'COLLECTION'))");
    expect(sql).toContain("CHECK (copy_type IN ('ORIGINAL', 'REPRINT'))");
  });

  it("binds local data to tenant, user, device, store, and till", () => {
    const scope = createOfflineScope({ tenantId: "TENANT", userId: "USER", deviceId: "DEVICE", storeId: "STORE", tillId: "TILL" });
    expect(scope.scopeKey).toBe("tenant:user:device:store:till");
  });

  it("normalizes all supported catalogue identifiers for local search", () => {
    expect(catalogueSearchText(item)).toBe("sku-001 blue widget 0123456789");
    expect(normalizeCatalogueSearch("  BLUE   widget ")).toBe("blue widget");
  });
});

describe("catalogue change synchronization", () => {
  it("resumes an interrupted cursor and commits the final server watermark", async () => {
    const applied: MobilePosCatalogueChangePage[] = [];
    let state: { watermarkUtc?: string; pageCursor?: string } = {
      watermarkUtc: "2026-10-08T00:00:00.000Z",
      pageCursor: "resume-page",
    };
    const port: CatalogueSyncPort = {
      loadState: async () => state,
      applyPage: async page => {
        applied.push(page);
        state = page.hasMore
          ? { watermarkUtc: state.watermarkUtc, pageCursor: page.nextCursor! }
          : { watermarkUtc: page.snapshotAtUtc };
      },
    };
    const requests: Array<{ watermarkUtc?: string; pageCursor?: string }> = [];
    const pages: MobilePosCatalogueChangePage[] = [
      { snapshotAtUtc: "2026-10-09T10:00:00.000Z", hasMore: true, nextCursor: "page-2", upserts: [item], tombstoneInventoryItemIds: [] },
      { snapshotAtUtc: "2026-10-09T10:00:00.000Z", hasMore: false, upserts: [], tombstoneInventoryItemIds: ["item-old"] },
    ];

    const count = await synchronizeCatalogue(port, async (watermarkUtc, pageCursor) => {
      requests.push({ watermarkUtc, pageCursor });
      return pages.shift()!;
    });

    expect(count).toBe(2);
    expect(requests).toEqual([
      { watermarkUtc: "2026-10-08T00:00:00.000Z", pageCursor: "resume-page" },
      { watermarkUtc: "2026-10-08T00:00:00.000Z", pageCursor: "page-2" },
    ]);
    expect(applied).toHaveLength(2);
    expect(state.watermarkUtc).toBe("2026-10-09T10:00:00.000Z");
  });

  it("rejects a nonterminal page without a continuation cursor", async () => {
    const port: CatalogueSyncPort = { loadState: async () => ({}), applyPage: async () => undefined };
    await expect(synchronizeCatalogue(port, async () => ({
      snapshotAtUtc: "2026-10-09T10:00:00.000Z",
      hasMore: true,
      upserts: [],
      tombstoneInventoryItemIds: [],
    }))).rejects.toThrow("continuation cursor");
  });
});

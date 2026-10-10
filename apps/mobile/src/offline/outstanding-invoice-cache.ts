import type { SQLiteDatabase } from "expo-sqlite";
import { activateOfflineScope, openOfflineDatabase, type OfflineScope } from "@/src/offline/database";
import type { OutstandingInvoice } from "@/src/types/api";

interface InvoiceRow {
  projection_json: string;
}

interface SnapshotRow {
  cached_at_utc: string;
}

export interface OutstandingInvoiceSnapshot {
  invoices: OutstandingInvoice[];
  cachedAtUtc: string;
}

export class SqliteOutstandingInvoiceCache {
  private constructor(private readonly database: SQLiteDatabase, readonly scope: OfflineScope) {}

  static async open(scope: OfflineScope): Promise<SqliteOutstandingInvoiceCache> {
    await activateOfflineScope(scope);
    return new SqliteOutstandingInvoiceCache(await openOfflineDatabase(), scope);
  }

  async replaceCustomerSnapshot(
    businessPartnerId: string,
    businessPartnerRoleId: string,
    invoices: OutstandingInvoice[],
    cachedAt = new Date(),
  ): Promise<OutstandingInvoiceSnapshot> {
    const partnerId = required(businessPartnerId, "business partner ID");
    const roleId = required(businessPartnerRoleId, "business partner role ID");
    const cachedAtUtc = validDate(cachedAt).toISOString();
    await this.database.withExclusiveTransactionAsync(async transaction => {
      await transaction.runAsync(
        "DELETE FROM outstanding_invoice_cache WHERE scope_key = ? AND business_partner_role_id = ?",
        this.scope.scopeKey,
        roleId,
      );
      await transaction.runAsync(
        `INSERT INTO outstanding_invoice_snapshot(
           scope_key, business_partner_id, business_partner_role_id, cached_at_utc)
         VALUES (?, ?, ?, ?)
         ON CONFLICT(scope_key, business_partner_role_id) DO UPDATE SET
           business_partner_id = excluded.business_partner_id,
           cached_at_utc = excluded.cached_at_utc`,
        this.scope.scopeKey,
        partnerId,
        roleId,
        cachedAtUtc,
      );
      for (const invoice of invoices) {
        await transaction.runAsync(
          `INSERT INTO outstanding_invoice_cache(
             scope_key, business_partner_id, business_partner_role_id, invoice_id,
             invoice_number, cached_at_utc, projection_json)
           VALUES (?, ?, ?, ?, ?, ?, ?)`,
          this.scope.scopeKey,
          partnerId,
          roleId,
          required(invoice.id, "invoice ID"),
          required(invoice.invoiceNumber, "invoice number"),
          cachedAtUtc,
          JSON.stringify(invoice),
        );
      }
    });
    return { invoices, cachedAtUtc };
  }

  async loadCustomerSnapshot(
    businessPartnerId: string,
    businessPartnerRoleId: string,
  ): Promise<OutstandingInvoiceSnapshot | null> {
    const partnerId = required(businessPartnerId, "business partner ID");
    const roleId = required(businessPartnerRoleId, "business partner role ID");
    const snapshot = await this.database.getFirstAsync<SnapshotRow>(
      `SELECT cached_at_utc
       FROM outstanding_invoice_snapshot
       WHERE scope_key = ? AND business_partner_id = ? AND business_partner_role_id = ?`,
      this.scope.scopeKey,
      partnerId,
      roleId,
    );
    if (!snapshot) return null;
    const rows = await this.database.getAllAsync<InvoiceRow>(
      `SELECT projection_json
       FROM outstanding_invoice_cache
       WHERE scope_key = ? AND business_partner_id = ? AND business_partner_role_id = ?
       ORDER BY invoice_number, invoice_id`,
      this.scope.scopeKey,
      partnerId,
      roleId,
    );
    return {
      invoices: rows.map(row => JSON.parse(row.projection_json) as OutstandingInvoice),
      cachedAtUtc: snapshot.cached_at_utc,
    };
  }
}

function required(value: string, label: string): string {
  const normalized = value?.trim();
  if (!normalized || normalized.length > 100) throw new Error(`The ${label} is invalid.`);
  return normalized;
}

function validDate(value: Date): Date {
  if (!(value instanceof Date) || !Number.isFinite(value.getTime())) throw new Error("The invoice cache timestamp is invalid.");
  return value;
}

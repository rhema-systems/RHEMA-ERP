import type { SQLiteDatabase } from "expo-sqlite";
import { activateOfflineScope, openOfflineDatabase, type OfflineScope } from "@/src/offline/database";
import type { MobilePosCollectionReceipt, MobilePosReceipt } from "@/src/types/api";

export type CanonicalMobilePosReceipt = MobilePosReceipt | MobilePosCollectionReceipt;
export type CanonicalReceiptKind = CanonicalMobilePosReceipt["receiptKind"];
export type CanonicalReceiptCopyType = CanonicalMobilePosReceipt["copyType"];

export interface CachedReceiptDocument {
  receipt: CanonicalMobilePosReceipt;
  receiptNumber: string;
  receivedAtUtc: string;
}

interface ReceiptRow {
  receipt_kind: string;
  receipt_id: string;
  copy_type: string;
  copy_number: number;
  receipt_number: string;
  received_at_utc: string;
  projection_json: string;
}

export class SqliteReceiptCache {
  private constructor(private readonly database: SQLiteDatabase, readonly scope: OfflineScope) {}

  static async open(scope: OfflineScope): Promise<SqliteReceiptCache> {
    await activateOfflineScope(scope);
    return new SqliteReceiptCache(await openOfflineDatabase(), scope);
  }

  static fromDatabase(database: SQLiteDatabase, scope: OfflineScope): SqliteReceiptCache {
    return new SqliteReceiptCache(database, scope);
  }

  async save(receipt: CanonicalMobilePosReceipt, receivedAt = new Date()): Promise<CachedReceiptDocument> {
    assertReceiptMatchesScope(receipt, this.scope);
    const receivedAtUtc = validDate(receivedAt).toISOString();
    const receiptNumber = canonicalReceiptNumber(receipt);
    await this.database.runAsync(
      `INSERT INTO receipt_document_cache(
         scope_key, receipt_kind, receipt_id, copy_type, copy_number, local_reference,
         receipt_number, occurred_at_utc, generated_at_utc, received_at_utc, projection_json)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
       ON CONFLICT(scope_key, receipt_kind, receipt_id, copy_type, copy_number) DO UPDATE SET
         local_reference = excluded.local_reference,
         receipt_number = excluded.receipt_number,
         occurred_at_utc = excluded.occurred_at_utc,
         generated_at_utc = excluded.generated_at_utc,
         received_at_utc = excluded.received_at_utc,
         projection_json = excluded.projection_json`,
      this.scope.scopeKey,
      receipt.receiptKind,
      required(receipt.receiptId, "receipt ID"),
      receipt.copyType,
      validCopyNumber(receipt),
      required(receipt.localReference, "local reference"),
      receiptNumber,
      validIso(receipt.occurredAtUtc, "receipt occurrence time"),
      validIso(receipt.generatedAtUtc, "receipt generation time"),
      receivedAtUtc,
      JSON.stringify(receipt),
    );
    return { receipt, receiptNumber, receivedAtUtc };
  }

  async get(
    receiptKind: CanonicalReceiptKind,
    receiptId: string,
    copyType: CanonicalReceiptCopyType = "ORIGINAL",
    copyNumber = 0,
  ): Promise<CachedReceiptDocument | null> {
    const row = await this.database.getFirstAsync<ReceiptRow>(
      `SELECT receipt_kind, receipt_id, copy_type, copy_number, receipt_number,
              received_at_utc, projection_json
       FROM receipt_document_cache
       WHERE scope_key = ? AND receipt_kind = ? AND receipt_id = ?
         AND copy_type = ? AND copy_number = ?`,
      this.scope.scopeKey,
      canonicalKind(receiptKind),
      required(receiptId, "receipt ID"),
      canonicalCopyType(copyType),
      normalizedCopyNumber(copyNumber),
    );
    return row ? mapRow(row, this.scope) : null;
  }

  async list(limit = 100): Promise<CachedReceiptDocument[]> {
    const rows = await this.database.getAllAsync<ReceiptRow>(
      `SELECT receipt_kind, receipt_id, copy_type, copy_number, receipt_number,
              received_at_utc, projection_json
       FROM receipt_document_cache
       WHERE scope_key = ?
       ORDER BY occurred_at_utc DESC, received_at_utc DESC
       LIMIT ?`,
      this.scope.scopeKey,
      Math.max(1, Math.min(Math.trunc(limit), 500)),
    );
    return rows.map(row => mapRow(row, this.scope));
  }
}

export function canonicalReceiptNumber(receipt: CanonicalMobilePosReceipt): string {
  return required(receipt.receiptKind === "SALE" ? receipt.invoiceNumber : receipt.localReference, "receipt number");
}

export function parseCanonicalReceipt(value: string): CanonicalMobilePosReceipt {
  let parsed: unknown;
  try {
    parsed = JSON.parse(value);
  } catch {
    throw new Error("The saved receipt contains unreadable data.");
  }
  if (!isRecord(parsed) || (parsed.receiptKind !== "SALE" && parsed.receiptKind !== "COLLECTION")) {
    throw new Error("The saved receipt is not a canonical Mobile POS receipt.");
  }
  if (parsed.copyType !== "ORIGINAL" && parsed.copyType !== "REPRINT") {
    throw new Error("The saved receipt copy type is invalid.");
  }
  if (!Array.isArray(parsed.tenders)
    || (parsed.receiptKind === "SALE" && !Array.isArray(parsed.lines))
    || (parsed.receiptKind === "COLLECTION" && !Array.isArray(parsed.allocations))) {
    throw new Error("The saved receipt detail is incomplete.");
  }
  return parsed as unknown as CanonicalMobilePosReceipt;
}

function mapRow(row: ReceiptRow, scope: OfflineScope): CachedReceiptDocument {
  const receipt = parseCanonicalReceipt(row.projection_json);
  assertReceiptMatchesScope(receipt, scope);
  if (receipt.receiptKind !== row.receipt_kind
    || receipt.receiptId !== row.receipt_id
    || receipt.copyType !== row.copy_type
    || receipt.copyNumber !== row.copy_number
    || canonicalReceiptNumber(receipt) !== row.receipt_number) {
    throw new Error("The saved receipt identity does not match its cache record.");
  }
  return { receipt, receiptNumber: row.receipt_number, receivedAtUtc: validIso(row.received_at_utc, "receipt cache time") };
}

function assertReceiptMatchesScope(receipt: CanonicalMobilePosReceipt, scope: OfflineScope): void {
  const matches = normalized(receipt.tenantId) === normalized(scope.tenantId)
    && normalized(receipt.deviceId) === normalized(scope.deviceId)
    && normalized(receipt.storeId) === normalized(scope.storeId)
    && normalized(receipt.tillId) === normalized(scope.tillId);
  if (!matches) throw new Error("The receipt does not belong to the active tenant, device, store, and till scope.");
  required(receipt.receiptId, "receipt ID");
  required(receipt.localReference, "local reference");
  validCopyNumber(receipt);
  validIso(receipt.occurredAtUtc, "receipt occurrence time");
  validIso(receipt.generatedAtUtc, "receipt generation time");
}

function canonicalKind(value: string): CanonicalReceiptKind {
  if (value !== "SALE" && value !== "COLLECTION") throw new Error("The receipt kind is invalid.");
  return value;
}

function canonicalCopyType(value: string): CanonicalReceiptCopyType {
  if (value !== "ORIGINAL" && value !== "REPRINT") throw new Error("The receipt copy type is invalid.");
  return value;
}

function validCopyNumber(receipt: CanonicalMobilePosReceipt): number {
  const copyNumber = normalizedCopyNumber(receipt.copyNumber);
  if ((receipt.copyType === "ORIGINAL" && copyNumber !== 0) || (receipt.copyType === "REPRINT" && copyNumber < 1)) {
    throw new Error("The receipt copy number does not match its copy type.");
  }
  return copyNumber;
}

function normalizedCopyNumber(value: number): number {
  const copyNumber = Math.trunc(value);
  if (!Number.isFinite(value) || copyNumber !== value || copyNumber < 0) throw new Error("The receipt copy number is invalid.");
  return copyNumber;
}

function validIso(value: string, label: string): string {
  const normalizedValue = required(value, label);
  if (!Number.isFinite(Date.parse(normalizedValue))) throw new Error(`The ${label} is invalid.`);
  return normalizedValue;
}

function validDate(value: Date): Date {
  if (!(value instanceof Date) || !Number.isFinite(value.getTime())) throw new Error("The receipt cache time is invalid.");
  return value;
}

function required(value: string, label: string): string {
  const normalizedValue = value?.trim();
  if (!normalizedValue || normalizedValue.length > 256) throw new Error(`The ${label} is required.`);
  return normalizedValue;
}

function normalized(value: string): string {
  return value?.trim().toLocaleLowerCase();
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

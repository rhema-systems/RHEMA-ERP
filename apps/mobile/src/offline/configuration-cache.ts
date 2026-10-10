import { activateOfflineScope, openOfflineDatabase, type OfflineScope } from "@/src/offline/database";
import type { MobilePosBankAccountOption, MobilePosBootstrap } from "@/src/types/api";

export async function cacheSessionConfiguration(scope: OfflineScope, bootstrap: MobilePosBootstrap): Promise<void> {
  await activateOfflineScope(scope);
  const database = await openOfflineDatabase();
  await database.withExclusiveTransactionAsync(async transaction => {
    await transaction.runAsync(
      `INSERT INTO configuration_cache(scope_key, configuration_key, changed_at_utc, projection_json)
       VALUES (?, 'bootstrap', ?, ?)
       ON CONFLICT(scope_key, configuration_key) DO UPDATE SET
         changed_at_utc = excluded.changed_at_utc,
         projection_json = excluded.projection_json`,
      scope.scopeKey,
      bootstrap.serverTimeUtc,
      JSON.stringify(bootstrap),
    );
    await transaction.runAsync("DELETE FROM payment_method_cache WHERE scope_key = ?", scope.scopeKey);
    for (const method of bootstrap.till.paymentMethods) {
      await transaction.runAsync(
        `INSERT INTO payment_method_cache(scope_key, payment_method_id, changed_at_utc, projection_json)
         VALUES (?, ?, ?, ?)`,
        scope.scopeKey,
        method.paymentMethodId,
        bootstrap.serverTimeUtc,
        JSON.stringify(method),
      );
    }
  });
}

export async function cacheBankAccounts(
  scope: OfflineScope,
  accounts: MobilePosBankAccountOption[],
  changedAt = new Date(),
): Promise<void> {
  await activateOfflineScope(scope);
  const changedAtUtc = validDate(changedAt).toISOString();
  await (await openOfflineDatabase()).runAsync(
    `INSERT INTO configuration_cache(scope_key, configuration_key, changed_at_utc, projection_json)
     VALUES (?, 'bank-accounts', ?, ?)
     ON CONFLICT(scope_key, configuration_key) DO UPDATE SET
       changed_at_utc = excluded.changed_at_utc,
       projection_json = excluded.projection_json`,
    scope.scopeKey,
    changedAtUtc,
    JSON.stringify(accounts),
  );
}

export async function loadBankAccounts(scope: OfflineScope): Promise<MobilePosBankAccountOption[] | null> {
  await activateOfflineScope(scope);
  const row = await (await openOfflineDatabase()).getFirstAsync<{ projection_json: string }>(
    "SELECT projection_json FROM configuration_cache WHERE scope_key = ? AND configuration_key = 'bank-accounts'",
    scope.scopeKey,
  );
  return row ? JSON.parse(row.projection_json) as MobilePosBankAccountOption[] : null;
}

function validDate(value: Date): Date {
  if (!(value instanceof Date) || !Number.isFinite(value.getTime())) throw new Error("The bank account cache timestamp is invalid.");
  return value;
}

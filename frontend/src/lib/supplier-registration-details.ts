export type RegistrationDataRecord = Record<string, unknown>;

export interface SupplierContactDetails {
  id?: string;
  contactName: string;
  contactTitle?: string;
  department?: string;
  email?: string;
  phone?: string;
  mobile?: string;
  isPrimary: boolean;
}

export interface SupplierBankAccountDetails {
  id?: string;
  bankName: string;
  bankBranch?: string;
  bankBranchCode?: string;
  accountNumber: string;
  accountName?: string;
  swiftCode?: string;
  iban?: string;
  currency?: string;
  isPrimary: boolean;
}

export function ensureOnePrimary<T extends { isPrimary: boolean }>(
  items: T[]
): T[] {
  if (items.length === 0) return [];
  const primaryIndex = Math.max(
    0,
    items.findIndex((item) => item.isPrimary)
  );
  return items.map((item, index) => ({
    ...item,
    isPrimary: index === primaryIndex,
  }));
}

function property(record: RegistrationDataRecord, ...keys: string[]) {
  const expected = new Set(keys.map((key) => key.toLowerCase()));
  const actualKey = Object.keys(record).find((key) =>
    expected.has(key.toLowerCase())
  );
  return actualKey ? record[actualKey] : undefined;
}

function text(record: RegistrationDataRecord, ...keys: string[]) {
  const value = property(record, ...keys);
  return typeof value === 'string' ? value.trim() : '';
}

function flag(record: RegistrationDataRecord, ...keys: string[]) {
  const value = property(record, ...keys);
  return value === true || value === 'true';
}

function records(record: RegistrationDataRecord, ...keys: string[]) {
  const value = property(record, ...keys);
  return Array.isArray(value)
    ? value.filter(
        (item): item is RegistrationDataRecord =>
          Boolean(item) && !Array.isArray(item) && typeof item === 'object'
      )
    : [];
}

export function parseRegistrationData(value?: string): RegistrationDataRecord {
  if (!value?.trim()) return {};

  try {
    let current: unknown = JSON.parse(value);
    for (let depth = 0; depth < 3; depth += 1) {
      if (!current || Array.isArray(current) || typeof current !== 'object') {
        break;
      }
      const record = current as RegistrationDataRecord;
      const nested = property(record, 'registrationData');
      if (typeof nested === 'string' && nested.trim()) {
        current = JSON.parse(nested);
        continue;
      }
      if (nested && !Array.isArray(nested) && typeof nested === 'object') {
        current = nested;
        continue;
      }
      break;
    }

    return current && !Array.isArray(current) && typeof current === 'object'
      ? (current as RegistrationDataRecord)
      : {};
  } catch {
    return {};
  }
}

export function registrationString(data: RegistrationDataRecord, key: string) {
  return text(data, key);
}

export function contactsFromRegistrationData(
  data: RegistrationDataRecord
): SupplierContactDetails[] {
  const contacts = records(data, 'contacts').map((contact) => ({
    id: text(contact, 'id') || undefined,
    contactName:
      text(contact, 'contactName', 'name') ||
      [text(contact, 'firstName'), text(contact, 'lastName')]
        .filter(Boolean)
        .join(' '),
    contactTitle:
      text(contact, 'contactTitle', 'title', 'position') || undefined,
    department: text(contact, 'department') || undefined,
    email: text(contact, 'email') || undefined,
    phone: text(contact, 'phone') || undefined,
    mobile: text(contact, 'mobile') || undefined,
    isPrimary: flag(contact, 'isPrimary'),
  }));

  if (contacts.length > 0) {
    return contacts;
  }

  const contactName = text(data, 'contactPersonName', 'contactPerson');
  const contactTitle = text(data, 'contactPersonTitle', 'contactTitle');
  const email = text(data, 'contactPersonEmail', 'contactEmail');
  const phone = text(data, 'contactPersonPhone', 'contactPhone');
  if (!contactName && !contactTitle && !email && !phone) {
    return [];
  }

  return [
    {
      contactName,
      contactTitle: contactTitle || undefined,
      email: email || undefined,
      phone: phone || undefined,
      isPrimary: true,
    },
  ];
}

export function bankAccountsFromRegistrationData(
  data: RegistrationDataRecord
): SupplierBankAccountDetails[] {
  const accounts = records(data, 'bankAccounts').map((account) => ({
    id: text(account, 'id') || undefined,
    bankName: text(account, 'bankName'),
    bankBranch: text(account, 'bankBranch', 'branchName') || undefined,
    bankBranchCode: text(account, 'bankBranchCode', 'branchCode') || undefined,
    accountNumber: text(account, 'accountNumber', 'bankAccountNumber') || '',
    accountName: text(account, 'accountName') || undefined,
    swiftCode: text(account, 'swiftCode') || undefined,
    iban: text(account, 'iban') || undefined,
    currency: text(account, 'currency', 'currencyCode') || undefined,
    isPrimary: flag(account, 'isPrimary'),
  }));

  if (accounts.length > 0) {
    return accounts;
  }

  const bankName = text(data, 'bankName');
  const accountNumber = text(data, 'accountNumber', 'bankAccountNumber');
  const bankBranch = text(data, 'bankBranch');
  const bankBranchCode = text(data, 'bankBranchCode');
  const accountName = text(data, 'accountName');
  const swiftCode = text(data, 'swiftCode');
  const iban = text(data, 'iban');
  const currency = text(data, 'currency', 'currencyCode');
  if (!bankName && !accountNumber && !bankBranch && !bankBranchCode) {
    return [];
  }

  return [
    {
      bankName,
      accountNumber,
      bankBranch: bankBranch || undefined,
      bankBranchCode: bankBranchCode || undefined,
      accountName: accountName || undefined,
      swiftCode: swiftCode || undefined,
      iban: iban || undefined,
      currency: currency || undefined,
      isPrimary: true,
    },
  ];
}

export function maskBankAccountNumber(accountNumber?: string) {
  const value = accountNumber?.trim() || '';
  if (!value) return 'Not provided';
  if (value.length <= 4) return value;
  return `${'•'.repeat(Math.min(8, value.length - 4))} ${value.slice(-4)}`;
}

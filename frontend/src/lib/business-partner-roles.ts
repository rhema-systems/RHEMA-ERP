// Keep legacy Both eligibility consistent with the server compatibility policy.
export const hasCustomerRole = (type?: string) =>
  ['Customer', 'Both', 'CustomerAndSupplier'].includes(type || '');
export const hasSupplierRole = (type?: string) =>
  ['Supplier', 'Vendor', 'Manufacturer', 'Both', 'CustomerAndSupplier'].includes(type || '');
export const hasContractorRole = (type?: string) =>
  ['Contractor', 'Both'].includes(type || '');

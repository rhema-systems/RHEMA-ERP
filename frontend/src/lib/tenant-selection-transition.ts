export interface TenantSelectionTransitionOptions {
  tenantCode: string;
  target: string;
  setCurrentTenantCode: (tenantCode: string) => void;
  cancelQueries: () => Promise<unknown>;
  removeQueries: () => void;
  replaceLocation?: (target: string) => void;
}

const replaceBrowserLocation = (target: string) => {
  if (typeof window === 'undefined') {
    return;
  }

  window.location.replace(target);
};

/**
 * Commits an authenticated tenant-context change at a full navigation boundary.
 *
 * The access token changes when a tenant is selected, so queries started with the
 * previous token must not be allowed to repopulate a shared client cache. A hard
 * replace also avoids an App Router transition being held behind those stale
 * requests while the tenant-selection page continues to display its spinner.
 */
export const completeTenantSelectionTransition = async ({
  tenantCode,
  target,
  setCurrentTenantCode,
  cancelQueries,
  removeQueries,
  replaceLocation = replaceBrowserLocation,
}: TenantSelectionTransitionOptions): Promise<void> => {
  setCurrentTenantCode(tenantCode);

  try {
    await cancelQueries();
  } catch (error) {
    console.warn('Unable to cancel queries during tenant selection:', error);
  }

  try {
    removeQueries();
  } catch (error) {
    console.warn('Unable to clear cached queries during tenant selection:', error);
  }

  replaceLocation(target);
};

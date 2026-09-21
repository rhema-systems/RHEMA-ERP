'use client';

import React from 'react';

import {
  estateLandManagementService,
  type EstateManagedAsset,
  type EstateManagedAssetQuery,
} from '@/services/estate-land-management.service';

export const ESTATE_TABLE_PAGE_SIZE = 10;

interface UseManagedAssetsPageOptions extends EstateManagedAssetQuery {
  errorMessage: string;
}

export function useManagedAssetsPage({
  errorMessage,
  ...query
}: UseManagedAssetsPageOptions) {
  const [assets, setAssets] = React.useState<EstateManagedAsset[]>([]);
  const [page, setPage] = React.useState(1);
  const [hasNextPage, setHasNextPage] = React.useState(false);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);

  const queryKey = JSON.stringify(query);

  const loadAssets = React.useCallback(
    async (nextPage = page) => {
      setIsLoading(true);
      setLoadError(null);
      try {
        const loadedAssets = await estateLandManagementService.getManagedAssets({
          ...query,
          skip: Math.max(0, nextPage - 1) * ESTATE_TABLE_PAGE_SIZE,
          take: ESTATE_TABLE_PAGE_SIZE + 1,
        });
        setAssets(loadedAssets.slice(0, ESTATE_TABLE_PAGE_SIZE));
        setHasNextPage(loadedAssets.length > ESTATE_TABLE_PAGE_SIZE);
      } catch {
        setAssets([]);
        setHasNextPage(false);
        setLoadError(errorMessage);
      } finally {
        setIsLoading(false);
      }
    },
    [errorMessage, page, queryKey]
  );

  React.useEffect(() => {
    void loadAssets(page);
  }, [loadAssets, page]);

  const totalPages = hasNextPage ? page + 1 : page;
  const totalItems = hasNextPage
    ? page * ESTATE_TABLE_PAGE_SIZE + 1
    : (page - 1) * ESTATE_TABLE_PAGE_SIZE + assets.length;

  return {
    assets,
    setAssets,
    page,
    setPage,
    hasNextPage,
    isLoading,
    loadError,
    loadAssets,
    pageSize: ESTATE_TABLE_PAGE_SIZE,
    totalPages,
    totalItems,
  };
}

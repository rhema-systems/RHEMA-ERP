import * as React from 'react';

export function usePaginatedItems<T>(items: readonly T[], pageSize = 10) {
  const [currentPage, setCurrentPage] = React.useState(1);
  const totalPages = Math.max(1, Math.ceil(items.length / pageSize));

  React.useEffect(() => {
    setCurrentPage((page) => Math.min(page, totalPages));
  }, [totalPages]);

  return {
    currentPage,
    setCurrentPage,
    totalPages,
    totalItems: items.length,
    pageSize,
    items: items.slice(
      (currentPage - 1) * pageSize,
      currentPage * pageSize
    ),
  };
}

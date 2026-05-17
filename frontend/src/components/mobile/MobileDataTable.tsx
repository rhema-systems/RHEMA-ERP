 
"use client"

import React, { useState, useMemo } from 'react'
import { cn } from '../../lib/utils'
import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Badge } from '../ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '../ui/sheet'
import { SwipeableCard } from './TouchInteractions'
import { ChevronDown, ChevronUp, Eye, Edit, Trash2, Filter, Search, MoreVertical, ArrowUpDown, SortAsc, SortDesc } from 'lucide-react'

interface Column<T = any> {
  key: string
  label: string
  sortable?: boolean
  width?: string
  render?: (value: any, row: T) => React.ReactNode
  mobile?: {
    primary?: boolean // Show in primary mobile view
    secondary?: boolean // Show in secondary details view
    hidden?: boolean // Hide on mobile entirely
  }
}

interface Action<T = any> {
  label: string
  icon: React.ComponentType<any>
  onClick: (row: T) => void
  variant?: 'default' | 'destructive' | 'outline' | 'secondary' | 'ghost' | 'link'
  color?: string
}

interface SwipeAction<T = any> {
  label: string
  icon: React.ComponentType<any>
  action: (row: T) => void
  color: 'red' | 'green' | 'blue' | 'orange'
}

interface MobileDataTableProps<T = any> {
  data: T[]
  columns: Column<T>[]
  actions?: Action<T>[]
  swipeActions?: {
    left?: SwipeAction<T>
    right?: SwipeAction<T>
  }
  searchable?: boolean
  sortable?: boolean
  filterable?: boolean
  filters?: {
    key: string
    label: string
    options: { value: string; label: string }[]
  }[]
  pageSize?: number
  loading?: boolean
  emptyMessage?: string
  onRowClick?: (row: T) => void
  className?: string
}

type SortDirection = 'asc' | 'desc' | null

export const MobileDataTable = <T extends Record<string, any>>({
  data,
  columns,
  actions = [],
  swipeActions,
  searchable = true,
  sortable = true,
  filterable = false,
  filters = [],
  pageSize = 10,
  loading = false,
  emptyMessage = "No data found",
  onRowClick,
  className
}: MobileDataTableProps<T>) => {
  const [searchTerm, setSearchTerm] = useState("")
  const [sortKey, setSortKey] = useState<string | null>(null)
  const [sortDirection, setSortDirection] = useState<SortDirection>(null)
  const [activeFilters, setActiveFilters] = useState<Record<string, string>>({})
  const [currentPage, setCurrentPage] = useState(1)
  const [viewMode, setViewMode] = useState<'cards' | 'table'>('cards')

  // Get columns for different mobile views
  const primaryColumns = columns.filter(col => col.mobile?.primary !== false && !col.mobile?.hidden)
  const secondaryColumns = columns.filter(col => col.mobile?.secondary && !col.mobile?.hidden)
  const allColumns = columns.filter(col => !col.mobile?.hidden)

  // Filter and search data
  const filteredData = useMemo(() => {
    let result = data

    // Apply search
    if (searchTerm) {
      result = result.filter(row => 
        columns.some(col => 
          String(row[col.key]).toLowerCase().includes(searchTerm.toLowerCase())
        )
      )
    }

    // Apply filters
    Object.entries(activeFilters).forEach(([key, value]) => {
      if (value && value !== 'all') {
        result = result.filter(row => String(row[key]) === value)
      }
    })

    // Apply sorting
    if (sortKey && sortDirection) {
      result.sort((a, b) => {
        const aVal = a[sortKey]
        const bVal = b[sortKey]
        
        if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1
        if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1
        return 0
      })
    }

    return result
  }, [data, searchTerm, activeFilters, sortKey, sortDirection, columns])

  // Pagination
  const totalPages = Math.ceil(filteredData.length / pageSize)
  const paginatedData = filteredData.slice(
    (currentPage - 1) * pageSize,
    currentPage * pageSize
  )

  const handleSort = (key: string) => {
    if (sortKey === key) {
      if (sortDirection === 'asc') {
        setSortDirection('desc')
      } else if (sortDirection === 'desc') {
        setSortKey(null)
        setSortDirection(null)
      }
    } else {
      setSortKey(key)
      setSortDirection('asc')
    }
  }

  const renderValue = (column: Column<T>, row: T) => {
    const value = row[column.key]
    if (column.render) {
      return column.render(value, row)
    }
    return String(value)
  }

  const renderCardView = () => (
    <div className="space-y-4">
      {paginatedData.map((row, index) => {
        const cardContent = (
          <Card key={index} className="transition-all duration-200 hover:shadow-md">
            <CardContent className="p-4">
              {/* Primary information */}
              <div className="space-y-2">
                {primaryColumns.slice(0, 2).map((column) => (
                  <div key={column.key}>
                    {column.key === primaryColumns[0]?.key ? (
                      <div className="font-semibold text-base">
                        {renderValue(column, row)}
                      </div>
                    ) : (
                      <div className="text-sm text-muted-foreground">
                        {renderValue(column, row)}
                      </div>
                    )}
                  </div>
                ))}
              </div>

              {/* Secondary information */}
              {primaryColumns.length > 2 && (
                <div className="flex flex-wrap gap-2 mt-3">
                  {primaryColumns.slice(2).map((column) => (
                    <Badge key={column.key} variant="secondary" className="text-xs">
                      {renderValue(column, row)}
                    </Badge>
                  ))}
                </div>
              )}

              {/* Actions */}
              {actions.length > 0 && (
                <div className="flex items-center justify-between mt-4 pt-3 border-t">
                  <div className="flex gap-2">
                    {actions.slice(0, 2).map((action, actionIndex) => (
                      <Button
                        key={actionIndex}
                        variant={action.variant || "outline"}
                        size="sm"
                        onClick={() => action.onClick(row)}
                        className="text-xs"
                      >
                        <action.icon className="h-3 w-3 mr-1" />
                        {action.label}
                      </Button>
                    ))}
                  </div>
                  
                  {actions.length > 2 && (
                    <Sheet>
                      <SheetTrigger asChild>
                        <Button variant="ghost" size="sm">
                          <MoreVertical className="h-4 w-4" />
                        </Button>
                      </SheetTrigger>
                      <SheetContent side="bottom" className="h-auto">
                        <SheetHeader>
                          <SheetTitle>Actions</SheetTitle>
                        </SheetHeader>
                        <div className="grid gap-2 pt-4">
                          {actions.map((action, actionIndex) => (
                            <Button
                              key={actionIndex}
                              variant={action.variant || "outline"}
                              onClick={() => action.onClick(row)}
                              className="justify-start"
                            >
                              <action.icon className="h-4 w-4 mr-2" />
                              {action.label}
                            </Button>
                          ))}
                        </div>
                      </SheetContent>
                    </Sheet>
                  )}
                </div>
              )}
            </CardContent>
          </Card>
        )

        // Wrap with swipeable if swipe actions are provided
        if (swipeActions) {
          const leftSwipeAction = swipeActions.left
          const rightSwipeAction = swipeActions.right
          return (
            <SwipeableCard
              key={index}
              leftAction={leftSwipeAction ? {
                ...leftSwipeAction,
                action: () => leftSwipeAction.action(row)
              } : undefined}
              rightAction={rightSwipeAction ? {
                ...rightSwipeAction,
                action: () => rightSwipeAction.action(row)
              } : undefined}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
            >
              {cardContent}
            </SwipeableCard>
          )
        }

        return (
          <div 
            key={index} 
            onClick={onRowClick ? () => onRowClick(row) : undefined}
            className={onRowClick ? "cursor-pointer" : ""}
          >
            {cardContent}
          </div>
        )
      })}
    </div>
  )

  const renderTableView = () => (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[600px]">
        <thead>
          <tr className="border-b">
            {allColumns.map((column) => (
              <th key={column.key} className="text-left p-3 font-medium">
                {sortable && column.sortable !== false ? (
                  <Button
                    variant="ghost"
                    onClick={() => handleSort(column.key)}
                    className="h-auto p-0 font-medium hover:bg-transparent"
                  >
                    {column.label}
                    {sortKey === column.key ? (
                      sortDirection === 'asc' ? (
                        <SortAsc className="ml-1 h-3 w-3" />
                      ) : (
                        <SortDesc className="ml-1 h-3 w-3" />
                      )
                    ) : (
                      <ArrowUpDown className="ml-1 h-3 w-3 opacity-50" />
                    )}
                  </Button>
                ) : (
                  column.label
                )}
              </th>
            ))}
            {actions.length > 0 && <th className="text-left p-3 font-medium">Actions</th>}
          </tr>
        </thead>
        <tbody>
          {paginatedData.map((row, index) => (
            <tr 
              key={index} 
              className={cn(
                "border-b hover:bg-muted/50 transition-colors",
                onRowClick && "cursor-pointer"
              )}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
            >
              {allColumns.map((column) => (
                <td key={column.key} className="p-3">
                  {renderValue(column, row)}
                </td>
              ))}
              {actions.length > 0 && (
                <td className="p-3">
                  <div className="flex gap-1">
                    {actions.slice(0, 2).map((action, actionIndex) => (
                      <Button
                        key={actionIndex}
                        variant={action.variant || "ghost"}
                        size="sm"
                        onClick={(e) => {
                          e.stopPropagation()
                          action.onClick(row)
                        }}
                      >
                        <action.icon className="h-3 w-3" />
                      </Button>
                    ))}
                    {actions.length > 2 && (
                      <Sheet>
                        <SheetTrigger asChild>
                          <Button variant="ghost" size="sm" onClick={(e) => e.stopPropagation()}>
                            <MoreVertical className="h-3 w-3" />
                          </Button>
                        </SheetTrigger>
                        <SheetContent side="bottom" className="h-auto">
                          <SheetHeader>
                            <SheetTitle>Actions</SheetTitle>
                          </SheetHeader>
                          <div className="grid gap-2 pt-4">
                            {actions.map((action, actionIndex) => (
                              <Button
                                key={actionIndex}
                                variant={action.variant || "outline"}
                                onClick={() => action.onClick(row)}
                                className="justify-start"
                              >
                                <action.icon className="h-4 w-4 mr-2" />
                                {action.label}
                              </Button>
                            ))}
                          </div>
                        </SheetContent>
                      </Sheet>
                    )}
                  </div>
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )

  const renderControls = () => (
    <div className="space-y-4">
      {/* Search and View Toggle */}
      <div className="flex gap-2">
        {searchable && (
          <div className="flex-1 relative">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
              placeholder="Search..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="pl-9"
            />
          </div>
        )}
        
        {/* View Mode Toggle */}
        <div className="flex bg-muted rounded-lg p-1">
          <Button
            variant={viewMode === 'cards' ? 'default' : 'ghost'}
            size="sm"
            onClick={() => setViewMode('cards')}
            className="px-3"
          >
            Cards
          </Button>
          <Button
            variant={viewMode === 'table' ? 'default' : 'ghost'}
            size="sm"
            onClick={() => setViewMode('table')}
            className="px-3"
          >
            Table
          </Button>
        </div>
      </div>

      {/* Filters */}
      {filterable && filters.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {filters.map((filter) => (
            <Select
              key={filter.key}
              value={activeFilters[filter.key] || "all"}
              onValueChange={(value) => 
                setActiveFilters(prev => ({
                  ...prev,
                  [filter.key]: value
                }))
              }
            >
              <SelectTrigger className="w-auto min-w-[120px]">
                <Filter className="h-3 w-3 mr-1" />
                <SelectValue placeholder={filter.label} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All {filter.label}</SelectItem>
                {filter.options.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          ))}
        </div>
      )}
    </div>
  )

  const renderPagination = () => {
    if (totalPages <= 1) return null

    return (
      <div className="flex items-center justify-between mt-6">
        <div className="text-sm text-muted-foreground">
          Showing {(currentPage - 1) * pageSize + 1} to{' '}
          {Math.min(currentPage * pageSize, filteredData.length)} of{' '}
          {filteredData.length} results
        </div>
        
        <div className="flex gap-1">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setCurrentPage(prev => Math.max(1, prev - 1))}
            disabled={currentPage === 1}
          >
            Previous
          </Button>
          
          {/* Show page numbers (simplified for mobile) */}
          {totalPages <= 5 ? (
            Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
              <Button
                key={page}
                variant={currentPage === page ? "default" : "outline"}
                size="sm"
                onClick={() => setCurrentPage(page)}
              >
                {page}
              </Button>
            ))
          ) : (
            <>
              <Button
                variant={currentPage === 1 ? "default" : "outline"}
                size="sm"
                onClick={() => setCurrentPage(1)}
              >
                1
              </Button>
              {currentPage > 3 && <span className="px-2">...</span>}
              {currentPage > 2 && currentPage < totalPages - 1 && (
                <Button variant="default" size="sm">
                  {currentPage}
                </Button>
              )}
              {currentPage < totalPages - 2 && <span className="px-2">...</span>}
              <Button
                variant={currentPage === totalPages ? "default" : "outline"}
                size="sm"
                onClick={() => setCurrentPage(totalPages)}
              >
                {totalPages}
              </Button>
            </>
          )}
          
          <Button
            variant="outline"
            size="sm"
            onClick={() => setCurrentPage(prev => Math.min(totalPages, prev + 1))}
            disabled={currentPage === totalPages}
          >
            Next
          </Button>
        </div>
      </div>
    )
  }

  if (loading) {
    return (
      <div className="space-y-4">
        {Array.from({ length: 3 }, (_, i) => (
          <Card key={i} className="animate-pulse">
            <CardContent className="p-4">
              <div className="h-4 bg-muted rounded w-3/4 mb-2"></div>
              <div className="h-3 bg-muted rounded w-1/2 mb-3"></div>
              <div className="flex gap-2">
                <div className="h-6 bg-muted rounded w-16"></div>
                <div className="h-6 bg-muted rounded w-16"></div>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    )
  }

  if (filteredData.length === 0) {
    return (
      <div className={cn("space-y-4", className)}>
        {(searchable || filterable) && renderControls()}
        <div className="text-center py-12">
          <div className="text-muted-foreground">{emptyMessage}</div>
        </div>
      </div>
    )
  }

  return (
    <div className={cn("space-y-4", className)}>
      {(searchable || filterable) && renderControls()}
      
      {viewMode === 'cards' ? renderCardView() : renderTableView()}
      
      {renderPagination()}
    </div>
  )
}

export default MobileDataTable

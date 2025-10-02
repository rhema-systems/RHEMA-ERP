'use client';

import React, { useState, useCallback } from 'react';
import { Card, CardContent } from '../ui/card';
import { Button } from '../ui/button';
import { Badge } from '../ui/badge';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '../ui/dropdown-menu';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '../ui/dialog';
import { 
  Grid3X3, 
  Settings, 
  Plus, 
  Move, 
  Trash2, 
  Eye, 
  EyeOff,
  MoreHorizontal,
  Maximize2,
  Minimize2
} from 'lucide-react';
import { cn } from '../../lib/utils';

export interface DashboardWidget {
  id: string;
  title: string;
  type: 'kpi' | 'chart' | 'table' | 'custom';
  size: 'sm' | 'md' | 'lg' | 'xl';
  position: { x: number; y: number };
  visible: boolean;
  config?: any;
  component: React.ComponentType<any>;
}

interface DashboardGridProps {
  widgets: DashboardWidget[];
  onUpdateWidget: (widgetId: string, updates: Partial<DashboardWidget>) => void;
  onRemoveWidget: (widgetId: string) => void;
  onAddWidget: (widget: Omit<DashboardWidget, 'id'>) => void;
  editMode?: boolean;
  className?: string;
}

const gridSizes = {
  sm: 'col-span-1 row-span-1',
  md: 'col-span-2 row-span-1',
  lg: 'col-span-2 row-span-2',
  xl: 'col-span-3 row-span-2'
};

const availableWidgetTypes = [
  { 
    type: 'kpi', 
    name: 'KPI Card', 
    description: 'Display key performance indicators',
    icon: '📊'
  },
  { 
    type: 'chart', 
    name: 'Chart Widget', 
    description: 'Various chart types and visualizations',
    icon: '📈'
  },
  { 
    type: 'table', 
    name: 'Data Table', 
    description: 'Tabular data with sorting and filtering',
    icon: '📋'
  },
  { 
    type: 'custom', 
    name: 'Custom Widget', 
    description: 'Custom component or integration',
    icon: '⚙️'
  }
];

export function DashboardGrid({
  widgets,
  onUpdateWidget,
  onRemoveWidget,
  onAddWidget,
  editMode = false,
  className
}: DashboardGridProps) {
  const [draggedWidget, setDraggedWidget] = useState<string | null>(null);
  const [isAddWidgetOpen, setIsAddWidgetOpen] = useState(false);

  const handleDragStart = useCallback((widgetId: string) => {
    if (editMode) {
      setDraggedWidget(widgetId);
    }
  }, [editMode]);

  const handleDragEnd = useCallback(() => {
    setDraggedWidget(null);
  }, []);

  const toggleWidgetVisibility = useCallback((widgetId: string) => {
    const widget = widgets.find(w => w.id === widgetId);
    if (widget) {
      onUpdateWidget(widgetId, { visible: !widget.visible });
    }
  }, [widgets, onUpdateWidget]);

  const changeWidgetSize = useCallback((widgetId: string, newSize: DashboardWidget['size']) => {
    onUpdateWidget(widgetId, { size: newSize });
  }, [onUpdateWidget]);

  const visibleWidgets = widgets.filter(widget => widget.visible);

  return (
    <div className={cn('w-full', className)}>
      {/* Grid Controls */}
      {editMode && (
        <div className="flex items-center justify-between mb-6 p-4 bg-muted/50 rounded-lg border border-dashed">
          <div className="flex items-center space-x-2">
            <Grid3X3 className="h-5 w-5 text-muted-foreground" />
            <span className="text-sm font-medium">Edit Mode</span>
            <Badge variant="outline">
              {visibleWidgets.length} widget{visibleWidgets.length !== 1 ? 's' : ''}
            </Badge>
          </div>
          
          <div className="flex items-center space-x-2">
            <Dialog open={isAddWidgetOpen} onOpenChange={setIsAddWidgetOpen}>
              <DialogTrigger asChild>
                <Button variant="outline" size="sm">
                  <Plus className="h-4 w-4 mr-2" />
                  Add Widget
                </Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>Add New Widget</DialogTitle>
                  <DialogDescription>
                    Choose a widget type to add to your dashboard
                  </DialogDescription>
                </DialogHeader>
                <div className="grid grid-cols-2 gap-4 mt-4">
                  {availableWidgetTypes.map((widgetType) => (
                    <Card 
                      key={widgetType.type}
                      className="cursor-pointer hover:shadow-md transition-shadow"
                      onClick={() => {
                        // This would typically open a configuration dialog
                        // For now, we'll just add a basic widget
                        const newWidget: Omit<DashboardWidget, 'id'> = {
                          title: `New ${widgetType.name}`,
                          type: widgetType.type as DashboardWidget['type'],
                          size: 'md',
                          position: { x: 0, y: 0 },
                          visible: true,
                          component: () => <div>Placeholder Widget</div>
                        };
                        onAddWidget(newWidget);
                        setIsAddWidgetOpen(false);
                      }}
                    >
                      <CardContent className="p-4">
                        <div className="text-center">
                          <div className="text-2xl mb-2">{widgetType.icon}</div>
                          <h3 className="font-medium text-sm">{widgetType.name}</h3>
                          <p className="text-xs text-muted-foreground mt-1">
                            {widgetType.description}
                          </p>
                        </div>
                      </CardContent>
                    </Card>
                  ))}
                </div>
              </DialogContent>
            </Dialog>
          </div>
        </div>
      )}

      {/* Widget Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6 auto-rows-max">
        {visibleWidgets.map((widget) => {
          const WidgetComponent = widget.component;
          
          return (
            <div
              key={widget.id}
              className={cn(
                'relative group transition-all duration-200',
                gridSizes[widget.size],
                editMode && 'ring-2 ring-muted',
                draggedWidget === widget.id && 'opacity-50 scale-95'
              )}
              draggable={editMode}
              onDragStart={() => handleDragStart(widget.id)}
              onDragEnd={handleDragEnd}
            >
              {/* Widget Controls */}
              {editMode && (
                <div className="absolute -top-2 -right-2 z-10 flex items-center space-x-1">
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button variant="outline" size="sm" className="h-6 w-6 p-0 bg-white shadow-md">
                        <MoreHorizontal className="h-3 w-3" />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      <DropdownMenuItem onClick={() => toggleWidgetVisibility(widget.id)}>
                        {widget.visible ? (
                          <>
                            <EyeOff className="h-4 w-4 mr-2" />
                            Hide Widget
                          </>
                        ) : (
                          <>
                            <Eye className="h-4 w-4 mr-2" />
                            Show Widget
                          </>
                        )}
                      </DropdownMenuItem>
                      
                      <DropdownMenuSeparator />
                      
                      <DropdownMenuItem onClick={() => changeWidgetSize(widget.id, 'sm')}>
                        <Minimize2 className="h-4 w-4 mr-2" />
                        Small
                      </DropdownMenuItem>
                      <DropdownMenuItem onClick={() => changeWidgetSize(widget.id, 'md')}>
                        Medium
                      </DropdownMenuItem>
                      <DropdownMenuItem onClick={() => changeWidgetSize(widget.id, 'lg')}>
                        Large
                      </DropdownMenuItem>
                      <DropdownMenuItem onClick={() => changeWidgetSize(widget.id, 'xl')}>
                        <Maximize2 className="h-4 w-4 mr-2" />
                        Extra Large
                      </DropdownMenuItem>
                      
                      <DropdownMenuSeparator />
                      
                      <DropdownMenuItem 
                        onClick={() => onRemoveWidget(widget.id)}
                        className="text-destructive"
                      >
                        <Trash2 className="h-4 w-4 mr-2" />
                        Remove Widget
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </div>
              )}

              {/* Drag Handle */}
              {editMode && (
                <div className="absolute top-2 right-2 z-10 opacity-0 group-hover:opacity-100 transition-opacity">
                  <Move className="h-4 w-4 text-muted-foreground cursor-move" />
                </div>
              )}

              {/* Widget Content */}
              <div className="h-full">
                <WidgetComponent 
                  title={widget.title}
                  config={widget.config}
                  size={widget.size}
                  editMode={editMode}
                />
              </div>
            </div>
          );
        })}

        {/* Empty State */}
        {visibleWidgets.length === 0 && (
          <div className="col-span-full">
            <Card className="border-dashed border-2">
              <CardContent className="flex flex-col items-center justify-center py-12">
                <Grid3X3 className="h-12 w-12 text-muted-foreground mb-4" />
                <h3 className="text-lg font-medium mb-2">No widgets added yet</h3>
                <p className="text-muted-foreground text-center mb-4">
                  {editMode 
                    ? "Click 'Add Widget' to get started with your dashboard" 
                    : "Enable edit mode to customize your dashboard"
                  }
                </p>
                {editMode && (
                  <Button onClick={() => setIsAddWidgetOpen(true)}>
                    <Plus className="h-4 w-4 mr-2" />
                    Add Your First Widget
                  </Button>
                )}
              </CardContent>
            </Card>
          </div>
        )}
      </div>
    </div>
  );
}

export default DashboardGrid;
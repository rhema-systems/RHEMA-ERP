import React, { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { Database, Zap, Settings, Users, Building } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

interface IntegrationNodeData {
  label: string;
  module?: string;
  action?: string;
}

export const IntegrationNode = memo<NodeProps<IntegrationNodeData>>(({ data }) => {
  const getModuleIcon = (module: string) => {
    switch (module) {
      case 'maintenance': return Settings;
      case 'inventory': return Database;
      case 'hr': return Users;
      case 'finance': return Building;
      default: return Zap;
    }
  };

  const getModuleName = (module: string) => {
    switch (module) {
      case 'maintenance': return 'Maintenance';
      case 'inventory': return 'Inventory';
      case 'hr': return 'HR';
      case 'finance': return 'Finance';
      case 'procurement': return 'Procurement';
      case 'projects': return 'Projects';
      case 'sales': return 'Sales';
      case 'quality': return 'Quality';
      default: return 'Integration';
    }
  };

  const getActionColor = (action: string) => {
    switch (action) {
      case 'create': return 'bg-green-100 text-green-800';
      case 'update': return 'bg-blue-100 text-blue-800';
      case 'delete': return 'bg-red-100 text-red-800';
      case 'notify': return 'bg-orange-100 text-orange-800';
      case 'validate': return 'bg-purple-100 text-purple-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  const IconComponent = getModuleIcon(data.module || '');

  return (
    <div className="px-3 py-2 shadow-md rounded-md bg-white border-2 border-indigo-300 min-w-[160px]">
      <Handle
        type="target"
        position={Position.Top}
        className="w-3 h-3 !bg-indigo-400"
      />
      
      <div className="flex items-center justify-center mb-1">
        <IconComponent className="h-4 w-4 mr-2 text-indigo-600" />
        <div className="text-sm font-medium text-center text-gray-800">
          {data.label || 'Integration'}
        </div>
      </div>
      
      {data.module && (
        <div className="flex justify-center mb-1">
          <Badge variant="outline" className="text-xs bg-indigo-100 text-indigo-800">
            {getModuleName(data.module)}
          </Badge>
        </div>
      )}
      
      {data.action && (
        <div className="flex justify-center mb-1">
          <Badge variant="outline" className={`text-xs ${getActionColor(data.action)}`}>
            {data.action.toUpperCase()}
          </Badge>
        </div>
      )}
      
      <Handle
        type="source"
        position={Position.Bottom}
        className="w-3 h-3 !bg-indigo-400"
      />
    </div>
  );
});

IntegrationNode.displayName = 'IntegrationNode';
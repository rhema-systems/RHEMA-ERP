import React, { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { FileText, User, Clock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

interface TaskNodeData {
  label: string;
  assignee?: string;
  priority?: 'low' | 'medium' | 'high';
  dueDate?: string;
  instructions?: string;
}

export const TaskNode = memo<NodeProps<TaskNodeData>>(({ data }) => {
  const getPriorityColor = (priority: string) => {
    switch (priority) {
      case 'high': return 'bg-red-100 text-red-800';
      case 'medium': return 'bg-yellow-100 text-yellow-800';
      case 'low': return 'bg-green-100 text-green-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  return (
    <div className="px-3 py-2 shadow-md rounded-md bg-white border-2 border-blue-300 min-w-[160px]">
      <Handle
        type="target"
        position={Position.Top}
        className="w-3 h-3 !bg-blue-400"
      />
      
      <div className="flex items-center justify-center mb-1">
        <FileText className="h-4 w-4 mr-2 text-blue-600" />
        <div className="text-sm font-medium text-center text-gray-800">
          {data.label || 'Task'}
        </div>
      </div>
      
      {data.priority && (
        <div className="flex justify-center mb-1">
          <Badge variant="outline" className={`text-xs ${getPriorityColor(data.priority)}`}>
            {data.priority.toUpperCase()}
          </Badge>
        </div>
      )}
      
      {data.assignee && (
        <div className="flex items-center justify-center text-xs text-gray-600 mb-1">
          <User className="h-3 w-3 mr-1" />
          <span className="truncate max-w-[100px]">{data.assignee}</span>
        </div>
      )}
      
      {data.dueDate && (
        <div className="flex items-center justify-center text-xs text-gray-600">
          <Clock className="h-3 w-3 mr-1" />
          <span>{data.dueDate}</span>
        </div>
      )}
      
      <Handle
        type="source"
        position={Position.Bottom}
        className="w-3 h-3 !bg-blue-400"
      />
    </div>
  );
});

TaskNode.displayName = 'TaskNode';
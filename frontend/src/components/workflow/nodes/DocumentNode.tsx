import React, { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { Upload, FileText, Shield } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

interface DocumentNodeData {
  label: string;
  documentType?: string;
  required?: boolean;
  approvalRequired?: boolean;
}

export const DocumentNode = memo<NodeProps<DocumentNodeData>>(({ data }) => {
  return (
    <div className="px-3 py-2 shadow-md rounded-md bg-white border-2 border-teal-300 min-w-[160px]">
      <Handle
        type="target"
        position={Position.Top}
        className="w-3 h-3 !bg-teal-400"
      />
      
      <div className="flex items-center justify-center mb-1">
        <Upload className="h-4 w-4 mr-2 text-teal-600" />
        <div className="text-sm font-medium text-center text-gray-800">
          {data.label || 'Document'}
        </div>
      </div>
      
      {data.documentType && (
        <div className="flex justify-center mb-1">
          <Badge variant="outline" className="text-xs bg-teal-100 text-teal-800">
            {data.documentType}
          </Badge>
        </div>
      )}
      
      <div className="flex justify-center space-x-1 mb-1">
        {data.required && (
          <div className="flex items-center px-1 py-0.5 bg-red-100 rounded text-xs text-red-800">
            <span>Required</span>
          </div>
        )}
        {data.approvalRequired && (
          <div className="flex items-center px-1 py-0.5 bg-purple-100 rounded text-xs text-purple-800">
            <Shield className="h-3 w-3 mr-1" />
            <span>Approval</span>
          </div>
        )}
      </div>
      
      <Handle
        type="source"
        position={Position.Bottom}
        className="w-3 h-3 !bg-teal-400"
      />
    </div>
  );
});

DocumentNode.displayName = 'DocumentNode';
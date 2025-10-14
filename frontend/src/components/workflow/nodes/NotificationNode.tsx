import React, { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { Bell, Mail, Phone, MessageSquare } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

interface NotificationNodeData {
  label: string;
  channels?: string[];
  template?: string;
  priority?: 'low' | 'normal' | 'high';
}

export const NotificationNode = memo<NodeProps<NotificationNodeData>>(({ data }) => {
  const getChannelIcon = (channel: string) => {
    switch (channel) {
      case 'email': return Mail;
      case 'sms': return Phone;
      case 'push': return Bell;
      case 'inapp': return MessageSquare;
      default: return Bell;
    }
  };

  const getPriorityColor = (priority: string) => {
    switch (priority) {
      case 'high': return 'bg-red-100 text-red-800';
      case 'normal': return 'bg-blue-100 text-blue-800';
      case 'low': return 'bg-gray-100 text-gray-800';
      default: return 'bg-blue-100 text-blue-800';
    }
  };

  return (
    <div className="px-3 py-2 shadow-md rounded-md bg-white border-2 border-orange-300 min-w-[160px]">
      <Handle
        type="target"
        position={Position.Top}
        className="w-3 h-3 !bg-orange-400"
      />
      
      <div className="flex items-center justify-center mb-1">
        <Bell className="h-4 w-4 mr-2 text-orange-600" />
        <div className="text-sm font-medium text-center text-gray-800">
          {data.label || 'Notification'}
        </div>
      </div>
      
      {data.priority && (
        <div className="flex justify-center mb-1">
          <Badge variant="outline" className={`text-xs ${getPriorityColor(data.priority)}`}>
            {data.priority.toUpperCase()}
          </Badge>
        </div>
      )}
      
      {data.template && (
        <div className="text-xs text-center text-gray-600 mb-1 truncate">
          {data.template}
        </div>
      )}
      
      {data.channels && data.channels.length > 0 && (
        <div className="flex justify-center space-x-1 mb-1">
          {data.channels.slice(0, 4).map((channel, index) => {
            const IconComponent = getChannelIcon(channel);
            return (
              <div key={index} className="p-1 bg-orange-100 rounded">
                <IconComponent className="h-3 w-3 text-orange-600" />
              </div>
            );
          })}
          {data.channels.length > 4 && (
            <div className="p-1 bg-gray-100 rounded">
              <span className="text-xs text-gray-600">+{data.channels.length - 4}</span>
            </div>
          )}
        </div>
      )}
      
      <Handle
        type="source"
        position={Position.Bottom}
        className="w-3 h-3 !bg-orange-400"
      />
    </div>
  );
});

NotificationNode.displayName = 'NotificationNode';
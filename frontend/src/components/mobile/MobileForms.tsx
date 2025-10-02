"use client"

import React, { useState, useRef, useEffect } from 'react'
import { cn } from '../../lib/utils'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { Button } from '../ui/button'
import { Textarea } from '../ui/textarea'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Switch } from '../ui/switch'
import { Check, ChevronDown, X } from 'lucide-react'

interface MobileInputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string
  error?: string
  fullWidth?: boolean
  touchOptimized?: boolean
}

export const MobileInput: React.FC<MobileInputProps> = ({
  label,
  error,
  fullWidth = true,
  touchOptimized = true,
  className,
  ...props
}) => {
  const [isFocused, setIsFocused] = useState(false)
  
  return (
    <div className={cn("space-y-2", fullWidth && "w-full")}>
      {label && (
        <Label 
          htmlFor={props.id} 
          className={cn(
            "text-sm font-medium",
            touchOptimized && "text-base" // Larger text for better readability on mobile
          )}
        >
          {label}
        </Label>
      )}
      <Input
        {...props}
        className={cn(
          "transition-all duration-200",
          touchOptimized && [
            "min-h-[44px]", // Minimum 44px touch target
            "text-base", // Prevent zoom on iOS
            "px-4 py-3" // More padding for easier touch
          ],
          isFocused && "ring-2 ring-primary/20",
          error && "border-red-500",
          className
        )}
        onFocus={(e) => {
          setIsFocused(true)
          props.onFocus?.(e)
        }}
        onBlur={(e) => {
          setIsFocused(false)
          props.onBlur?.(e)
        }}
      />
      {error && (
        <p className="text-sm text-red-600">{error}</p>
      )}
    </div>
  )
}

interface MobileTextareaProps extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  label?: string
  error?: string
  fullWidth?: boolean
  touchOptimized?: boolean
  autoResize?: boolean
}

export const MobileTextarea: React.FC<MobileTextareaProps> = ({
  label,
  error,
  fullWidth = true,
  touchOptimized = true,
  autoResize = false,
  className,
  ...props
}) => {
  const [isFocused, setIsFocused] = useState(false)
  const textareaRef = useRef<HTMLTextAreaElement>(null)

  useEffect(() => {
    if (autoResize && textareaRef.current) {
      const textarea = textareaRef.current
      textarea.style.height = 'auto'
      textarea.style.height = `${textarea.scrollHeight}px`
    }
  }, [props.value, autoResize])

  return (
    <div className={cn("space-y-2", fullWidth && "w-full")}>
      {label && (
        <Label 
          htmlFor={props.id}
          className={cn(
            "text-sm font-medium",
            touchOptimized && "text-base"
          )}
        >
          {label}
        </Label>
      )}
      <Textarea
        ref={textareaRef}
        {...props}
        className={cn(
          "transition-all duration-200",
          touchOptimized && [
            "min-h-[88px]", // Double the minimum touch target for multi-line
            "text-base",
            "px-4 py-3"
          ],
          isFocused && "ring-2 ring-primary/20",
          error && "border-red-500",
          autoResize && "resize-none",
          className
        )}
        onFocus={(e) => {
          setIsFocused(true)
          props.onFocus?.(e)
        }}
        onBlur={(e) => {
          setIsFocused(false)
          props.onBlur?.(e)
        }}
        onChange={(e) => {
          if (autoResize && textareaRef.current) {
            const textarea = textareaRef.current
            textarea.style.height = 'auto'
            textarea.style.height = `${textarea.scrollHeight}px`
          }
          props.onChange?.(e)
        }}
      />
      {error && (
        <p className="text-sm text-red-600">{error}</p>
      )}
    </div>
  )
}

interface MobileSwitchProps {
  label: string
  description?: string
  checked: boolean
  onCheckedChange: (checked: boolean) => void
  touchOptimized?: boolean
}

export const MobileSwitch: React.FC<MobileSwitchProps> = ({
  label,
  description,
  checked,
  onCheckedChange,
  touchOptimized = true
}) => {
  return (
    <div 
      className={cn(
        "flex items-center justify-between p-4 rounded-lg border bg-card cursor-pointer",
        touchOptimized && "min-h-[60px]" // Ensure adequate touch area
      )}
      onClick={() => onCheckedChange(!checked)}
    >
      <div className="flex-1 pr-4">
        <Label className={cn("font-medium cursor-pointer", touchOptimized && "text-base")}>
          {label}
        </Label>
        {description && (
          <p className="text-sm text-muted-foreground mt-1">{description}</p>
        )}
      </div>
      <Switch checked={checked} onCheckedChange={onCheckedChange} />
    </div>
  )
}

// MobileDatePicker removed - requires calendar component

interface MobileSelectProps {
  label: string
  value: string
  onValueChange: (value: string) => void
  options: { value: string; label: string }[]
  placeholder?: string
  error?: string
  touchOptimized?: boolean
}

export const MobileSelect: React.FC<MobileSelectProps> = ({
  label,
  value,
  onValueChange,
  options,
  placeholder = "Select option",
  error,
  touchOptimized = true
}) => {
  return (
    <div className="space-y-2 w-full">
      <Label className={cn("text-sm font-medium", touchOptimized && "text-base")}>
        {label}
      </Label>
      <Select value={value} onValueChange={onValueChange}>
        <SelectTrigger 
          className={cn(
            touchOptimized && "min-h-[44px] text-base px-4 py-3",
            error && "border-red-500"
          )}
        >
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {options.map((option) => (
            <SelectItem 
              key={option.value} 
              value={option.value}
              className={touchOptimized ? "min-h-[44px] text-base" : ""}
            >
              {option.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {error && (
        <p className="text-sm text-red-600">{error}</p>
      )}
    </div>
  )
}

interface MobileMultiSelectProps {
  label: string
  value: string[]
  onChange: (value: string[]) => void
  options: { value: string; label: string }[]
  placeholder?: string
  error?: string
  touchOptimized?: boolean
}

export const MobileMultiSelect: React.FC<MobileMultiSelectProps> = ({
  label,
  value,
  onChange,
  options,
  placeholder = "Select options",
  error,
  touchOptimized = true
}) => {
  const [isOpen, setIsOpen] = useState(false)

  const toggleOption = (optionValue: string) => {
    const newValue = value.includes(optionValue)
      ? value.filter(v => v !== optionValue)
      : [...value, optionValue]
    onChange(newValue)
  }

  const removeOption = (optionValue: string) => {
    onChange(value.filter(v => v !== optionValue))
  }

  const selectedLabels = value.map(v => 
    options.find(opt => opt.value === v)?.label || v
  )

  return (
    <div className="space-y-2 w-full">
      <Label className={cn("text-sm font-medium", touchOptimized && "text-base")}>
        {label}
      </Label>
      
      {/* Selected items */}
      {value.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {selectedLabels.map((label, index) => (
            <div 
              key={index}
              className="inline-flex items-center gap-1 px-2 py-1 bg-primary/10 text-primary rounded-full text-sm"
            >
              <span>{label}</span>
              <button
                onClick={() => removeOption(value[index])}
                className="hover:bg-primary/20 rounded-full p-1"
              >
                <X className="h-3 w-3" />
              </button>
            </div>
          ))}
        </div>
      )}
      
      {/* Dropdown trigger */}
      <Button
        type="button"
        variant="outline"
        onClick={() => setIsOpen(!isOpen)}
        className={cn(
          "w-full justify-between",
          touchOptimized && "min-h-[44px] text-base px-4 py-3",
          error && "border-red-500"
        )}
      >
        <span className={value.length === 0 ? "text-muted-foreground" : ""}>
          {value.length === 0 ? placeholder : `${value.length} selected`}
        </span>
        <ChevronDown className="h-4 w-4" />
      </Button>
      
      {/* Options dropdown */}
      {isOpen && (
        <div className="border rounded-md bg-popover text-popover-foreground shadow-md">
          {options.map((option) => (
            <div
              key={option.value}
              className={cn(
                "flex items-center gap-3 px-4 py-3 cursor-pointer hover:bg-accent",
                touchOptimized && "min-h-[44px]"
              )}
              onClick={() => toggleOption(option.value)}
            >
              <div className={cn(
                "w-5 h-5 border-2 rounded flex items-center justify-center",
                value.includes(option.value) && "bg-primary border-primary"
              )}>
                {value.includes(option.value) && (
                  <Check className="h-3 w-3 text-primary-foreground" />
                )}
              </div>
              <span className={touchOptimized ? "text-base" : "text-sm"}>
                {option.label}
              </span>
            </div>
          ))}
        </div>
      )}
      
      {error && (
        <p className="text-sm text-red-600">{error}</p>
      )}
    </div>
  )
}

interface MobileFormProps {
  children: React.ReactNode
  onSubmit?: (e: React.FormEvent) => void
  className?: string
  touchOptimized?: boolean
}

export const MobileForm: React.FC<MobileFormProps> = ({
  children,
  onSubmit,
  className,
  touchOptimized = true
}) => {
  return (
    <form 
      onSubmit={onSubmit}
      className={cn(
        "space-y-4",
        touchOptimized && "space-y-6", // More spacing for mobile
        className
      )}
    >
      {children}
    </form>
  )
}

interface MobileFormSectionProps {
  title?: string
  description?: string
  children: React.ReactNode
  touchOptimized?: boolean
}

export const MobileFormSection: React.FC<MobileFormSectionProps> = ({
  title,
  description,
  children,
  touchOptimized = true
}) => {
  return (
    <div className={cn("space-y-4", touchOptimized && "space-y-6")}>
      {(title || description) && (
        <div className="space-y-1">
          {title && (
            <h3 className={cn(
              "font-medium",
              touchOptimized ? "text-lg" : "text-base"
            )}>
              {title}
            </h3>
          )}
          {description && (
            <p className={cn(
              "text-muted-foreground",
              touchOptimized ? "text-base" : "text-sm"
            )}>
              {description}
            </p>
          )}
        </div>
      )}
      <div className={cn("space-y-4", touchOptimized && "space-y-6")}>
        {children}
      </div>
    </div>
  )
}
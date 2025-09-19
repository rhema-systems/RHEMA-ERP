import * as React from "react"
import { Slot } from "@radix-ui/react-slot"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "../../lib/utils"

const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-xl text-sm font-semibold ring-offset-background transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        default:
          "bg-gradient-to-r from-blue-600 to-indigo-600 text-primary-foreground shadow-lg hover:from-blue-700 hover:to-indigo-700 hover:shadow-xl active:scale-95 transition-all duration-200",
        destructive:
          "bg-gradient-to-r from-red-600 to-red-700 text-destructive-foreground shadow-lg hover:from-red-700 hover:to-red-800 hover:shadow-xl active:scale-95",
        outline:
          "border-2 border-border bg-background shadow-sm hover:bg-accent hover:text-accent-foreground hover:border-accent-foreground/20 active:scale-95",
        secondary:
          "bg-gradient-to-r from-slate-200 to-slate-300 text-secondary-foreground shadow-sm hover:from-slate-300 hover:to-slate-400 hover:shadow-md active:scale-95",
        ghost: "hover:bg-accent hover:text-accent-foreground active:scale-95",
        link: "text-primary underline-offset-4 hover:underline",
        success:
          "bg-gradient-to-r from-green-600 to-emerald-600 text-white shadow-lg hover:from-green-700 hover:to-emerald-700 hover:shadow-xl active:scale-95",
        warning:
          "bg-gradient-to-r from-amber-500 to-orange-500 text-white shadow-lg hover:from-amber-600 hover:to-orange-600 hover:shadow-xl active:scale-95",
      },
      size: {
        default: "h-11 px-6 py-2",
        sm: "h-9 rounded-lg px-4",
        lg: "h-13 rounded-xl px-8 text-base",
        icon: "h-10 w-10 rounded-xl",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
)

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean
  loading?: boolean
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, loading, children, disabled, ...props }, ref) => {
    const Comp = asChild ? Slot : "button"
    return (
      <Comp
        className={cn(buttonVariants({ variant, size, className }))}
        ref={ref}
        disabled={disabled || loading}
        {...props}
      >
        {loading && (
          <div className="animate-spin rounded-full h-4 w-4 border-2 border-current border-t-transparent" />
        )}
        {children}
      </Comp>
    )
  }
)
Button.displayName = "Button"

export { Button, buttonVariants }
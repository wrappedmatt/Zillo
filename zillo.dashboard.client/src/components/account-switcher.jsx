import * as React from "react"
import { ChevronsUpDown, Plus, Check } from "lucide-react"
import { useAccount } from "@/contexts/AccountContext"

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from "@/components/ui/sidebar"

export function AccountSwitcher() {
  const { isMobile } = useSidebar()
  const { accounts, currentAccount, switchAccount, loading } = useAccount()
  const [switching, setSwitching] = React.useState(false)

  const handleSwitchAccount = async (account) => {
    if (account.id === currentAccount?.id) return

    setSwitching(true)
    try {
      await switchAccount(account.id)
      // Reload the page to refresh all data for the new account
      window.location.reload()
    } catch (error) {
      console.error('Failed to switch account:', error)
    } finally {
      setSwitching(false)
    }
  }

  const getInitials = (name) => {
    if (!name) return "ZA"
    return name.substring(0, 2).toUpperCase()
  }

  const getRoleLabel = (role) => {
    switch (role) {
      case 'owner': return 'Owner'
      case 'admin': return 'Admin'
      case 'user': return 'Viewer'
      default: return role
    }
  }

  if (loading) {
    return (
      <SidebarMenu>
        <SidebarMenuItem>
          <SidebarMenuButton size="lg" className="animate-pulse">
            <div className="flex aspect-square size-8 items-center justify-center rounded-lg bg-sidebar-primary/50" />
            <div className="grid flex-1 gap-1">
              <div className="h-4 w-24 rounded bg-sidebar-primary/30" />
              <div className="h-3 w-16 rounded bg-sidebar-primary/20" />
            </div>
          </SidebarMenuButton>
        </SidebarMenuItem>
      </SidebarMenu>
    )
  }

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <SidebarMenuButton
              size="lg"
              className="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
              disabled={switching}
            >
              <div className="flex aspect-square size-8 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
                <span className="text-sm font-semibold">
                  {getInitials(currentAccount?.companyName)}
                </span>
              </div>
              <div className="grid flex-1 text-left text-sm leading-tight">
                <span className="truncate font-semibold">
                  {currentAccount?.companyName || "Select Account"}
                </span>
                <span className="truncate text-xs text-muted-foreground">
                  {currentAccount?.role ? getRoleLabel(currentAccount.role) : "No account selected"}
                </span>
              </div>
              <ChevronsUpDown className="ml-auto" />
            </SidebarMenuButton>
          </DropdownMenuTrigger>
          <DropdownMenuContent
            className="w-[--radix-dropdown-menu-trigger-width] min-w-56 rounded-lg"
            align="start"
            side={isMobile ? "bottom" : "right"}
            sideOffset={4}
          >
            <DropdownMenuLabel className="text-xs text-muted-foreground">
              Your Accounts
            </DropdownMenuLabel>
            {accounts.map((account) => (
              <DropdownMenuItem
                key={account.id}
                onClick={() => handleSwitchAccount(account)}
                className="gap-2 p-2 cursor-pointer"
                disabled={switching}
              >
                <div className="flex size-6 items-center justify-center rounded-sm border bg-background">
                  <span className="text-xs font-medium">
                    {getInitials(account.companyName)}
                  </span>
                </div>
                <div className="flex flex-col flex-1">
                  <span className="font-medium">{account.companyName}</span>
                  <span className="text-xs text-muted-foreground">
                    {getRoleLabel(account.role)}
                  </span>
                </div>
                {account.id === currentAccount?.id && (
                  <Check className="size-4 text-primary" />
                )}
              </DropdownMenuItem>
            ))}
            {accounts.length === 0 && (
              <DropdownMenuItem disabled className="text-muted-foreground">
                No accounts available
              </DropdownMenuItem>
            )}
            <DropdownMenuSeparator />
            <DropdownMenuItem className="gap-2 p-2 cursor-pointer" disabled>
              <div className="flex size-6 items-center justify-center rounded-md border border-dashed bg-background">
                <Plus className="size-4" />
              </div>
              <div className="font-medium text-muted-foreground">Create account</div>
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  )
}

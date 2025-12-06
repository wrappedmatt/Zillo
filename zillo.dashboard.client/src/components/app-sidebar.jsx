import * as React from "react"
import {
  Users,
  CreditCard,
  Settings2,
  Home,
  Citrus,
  Smartphone,
  BarChart3,
  MapPin,
} from "lucide-react"
import { useAuth } from '@/contexts/AuthContext'
import { useLocation } from 'react-router-dom'
import { AccountSwitcher } from "@/components/account-switcher"
import { NavMain } from "@/components/nav-main"
import { NavUser } from "@/components/nav-user"
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarRail,
} from "@/components/ui/sidebar"

export function AppSidebar({
  account,
  ...props
}) {
  const { user } = useAuth()
  const location = useLocation()

  const data = {
    user: {
      name: account?.company_name || "User",
      email: user?.email || "",
      avatar: null,
    },
    navMain: [
      {
        title: "Dashboard",
        url: "/dashboard",
        icon: Home,
        isActive: location.pathname === "/dashboard",
      },
      {
        title: "Customers",
        url: "/customers",
        icon: Users,
        isActive: location.pathname.startsWith("/customers"),
      },
      {
        title: "Terminals",
        url: "/terminals",
        icon: Smartphone,
        isActive: location.pathname.startsWith("/terminals"),
      },
      {
        title: "Transactions",
        url: "/transactions",
        icon: CreditCard,
        isActive: location.pathname.startsWith("/transactions"),
      },
      {
        title: "Reporting",
        url: "/reporting",
        icon: BarChart3,
        isActive: location.pathname.startsWith("/reporting"),
      },
      {
        title: "Locations",
        url: "/locations",
        icon: MapPin,
        isActive: location.pathname.startsWith("/locations"),
      },
      {
        title: "Settings",
        url: "/settings",
        icon: Settings2,
        isActive: location.pathname.startsWith("/settings"),
      },
    ],
  }

  return (
    <Sidebar collapsible="icon" {...props}>
      <SidebarHeader>
        <AccountSwitcher accounts={account ? [account] : []} activeAccount={account} />
      </SidebarHeader>
      <SidebarContent>
        <NavMain items={data.navMain} />
      </SidebarContent>
      <SidebarFooter>
        <NavUser user={data.user} />
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  );
}

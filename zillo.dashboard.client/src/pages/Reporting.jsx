import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '@/contexts/AuthContext'
import { supabase } from '@/lib/supabase'
import { AppSidebar } from '@/components/app-sidebar'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb'
import { Separator } from '@/components/ui/separator'
import {
  SidebarInset,
  SidebarProvider,
  SidebarTrigger,
} from '@/components/ui/sidebar'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Alert, AlertDescription } from '@/components/ui/alert'
import {
  Loader2,
  BarChart3,
  DollarSign,
  TrendingUp,
  Users,
  Activity,
  AlertTriangle,
  Calendar,
  Download
} from 'lucide-react'

export default function Reporting() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [account, setAccount] = useState(null)
  const [reports, setReports] = useState({
    totalCustomers: 0,
    totalPointsIssued: 0,
    totalPointsRedeemed: 0,
    outstandingLiability: 0,
    avgPointsPerCustomer: 0,
    totalTransactions: 0,
    unclaimedPoints: 0,
    activeCustomers: 0
  })

  useEffect(() => {
    document.title = 'Reporting | Zillo'
  }, [])

  useEffect(() => {
    if (!user) {
      navigate('/signin')
      return
    }
    loadReports()
  }, [user, navigate])

  const loadReports = async () => {
    try {
      setLoading(true)
      setError('')
      const { data: { session } } = await supabase.auth.getSession()

      if (!session) {
        navigate('/signin')
        return
      }

      const response = await fetch('/api/reports/summary', {
        headers: {
          'Authorization': `Bearer ${session.access_token}`
        }
      })

      if (!response.ok) {
        throw new Error('Failed to load reports')
      }

      const data = await response.json()
      setReports(data)

      // Also load account info
      const accountResponse = await fetch('/api/accounts/me', {
        headers: {
          'Authorization': `Bearer ${session.access_token}`
        }
      })

      if (accountResponse.ok) {
        const accountData = await accountResponse.json()
        setAccount(accountData)
      }
    } catch (error) {
      console.error('Error loading reports:', error)
      setError(error.message)
    } finally {
      setLoading(false)
    }
  }

  const formatCurrency = (amount) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 2
    }).format(amount)
  }

  const formatNumber = (num) => {
    return new Intl.NumberFormat('en-US').format(num)
  }

  if (loading) {
    return (
      <SidebarProvider>
        <AppSidebar account={account} />
        <SidebarInset>
          <div className="flex items-center justify-center h-screen">
            <Loader2 className="h-8 w-8 animate-spin text-indigo-600" />
          </div>
        </SidebarInset>
      </SidebarProvider>
    )
  }

  return (
    <SidebarProvider>
      <AppSidebar account={account} />
      <SidebarInset>
        <header className="flex h-16 shrink-0 items-center gap-2 transition-[width,height] ease-linear group-has-[[data-collapsible=icon]]/sidebar-wrapper:h-12">
          <div className="flex items-center gap-2 px-4">
            <SidebarTrigger className="-ml-1" />
            <Separator orientation="vertical" className="mr-2 h-4" />
            <Breadcrumb>
              <BreadcrumbList>
                <BreadcrumbItem className="hidden md:block">
                  <BreadcrumbLink href="/dashboard">
                    Dashboard
                  </BreadcrumbLink>
                </BreadcrumbItem>
                <BreadcrumbSeparator className="hidden md:block" />
                <BreadcrumbItem>
                  <BreadcrumbPage>Reporting</BreadcrumbPage>
                </BreadcrumbItem>
              </BreadcrumbList>
            </Breadcrumb>
          </div>
        </header>

        <div className="flex flex-1 flex-col gap-4 p-4 pt-0">
          {/* Header Card */}
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="flex h-12 w-12 items-center justify-center rounded-full bg-indigo-100">
                    <BarChart3 className="h-6 w-6 text-indigo-600" />
                  </div>
                  <div>
                    <CardTitle>Reports & Analytics</CardTitle>
                    <CardDescription>
                      View loyalty program metrics and liability reporting
                    </CardDescription>
                  </div>
                </div>
                {/* Future: Export button */}
              </div>
            </CardHeader>
          </Card>

          {/* Error Alert */}
          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {/* Key Metrics Grid */}
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
            {/* Total Customers */}
            <Card>
              <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                <CardTitle className="text-sm font-medium">Total Customers</CardTitle>
                <Users className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">{formatNumber(reports.totalCustomers)}</div>
                <p className="text-xs text-muted-foreground mt-1">
                  {formatNumber(reports.activeCustomers)} active this month
                </p>
              </CardContent>
            </Card>

            {/* Outstanding Liability */}
            <Card className="border-orange-200 bg-orange-50">
              <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                <CardTitle className="text-sm font-medium text-orange-900">Outstanding Liability</CardTitle>
                <AlertTriangle className="h-4 w-4 text-orange-600" />
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold text-orange-900">
                  {formatCurrency(reports.outstandingLiability)}
                </div>
                <p className="text-xs text-orange-700 mt-1">
                  {formatNumber(reports.totalPointsIssued - reports.totalPointsRedeemed)} points outstanding
                </p>
              </CardContent>
            </Card>

            {/* Total Points Issued */}
            <Card>
              <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                <CardTitle className="text-sm font-medium">Total Points Issued</CardTitle>
                <TrendingUp className="h-4 w-4 text-green-600" />
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold text-green-600">
                  {formatNumber(reports.totalPointsIssued)}
                </div>
                <p className="text-xs text-muted-foreground mt-1">
                  All-time points earned
                </p>
              </CardContent>
            </Card>

            {/* Total Points Redeemed */}
            <Card>
              <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                <CardTitle className="text-sm font-medium">Total Points Redeemed</CardTitle>
                <Activity className="h-4 w-4 text-red-600" />
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold text-red-600">
                  {formatNumber(reports.totalPointsRedeemed)}
                </div>
                <p className="text-xs text-muted-foreground mt-1">
                  All-time points used
                </p>
              </CardContent>
            </Card>
          </div>

          {/* Liability Report */}
          <Card>
            <CardHeader>
              <div className="flex items-center gap-2">
                <DollarSign className="h-5 w-5 text-orange-600" />
                <div>
                  <CardTitle>Liability Report</CardTitle>
                  <CardDescription>
                    Outstanding points represent potential future redemptions
                  </CardDescription>
                </div>
              </div>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {/* Liability Breakdown */}
                <div className="grid gap-4 md:grid-cols-3">
                  <div className="p-4 bg-green-50 rounded-lg border border-green-200">
                    <p className="text-sm font-medium text-green-900 mb-1">Points Issued</p>
                    <p className="text-2xl font-bold text-green-600">{formatNumber(reports.totalPointsIssued)}</p>
                    <p className="text-xs text-green-700 mt-1">
                      Total earned by customers
                    </p>
                  </div>

                  <div className="p-4 bg-red-50 rounded-lg border border-red-200">
                    <p className="text-sm font-medium text-red-900 mb-1">Points Redeemed</p>
                    <p className="text-2xl font-bold text-red-600">{formatNumber(reports.totalPointsRedeemed)}</p>
                    <p className="text-xs text-red-700 mt-1">
                      Total used by customers
                    </p>
                  </div>

                  <div className="p-4 bg-orange-50 rounded-lg border border-orange-200">
                    <p className="text-sm font-medium text-orange-900 mb-1">Outstanding Liability</p>
                    <p className="text-2xl font-bold text-orange-600">
                      {formatNumber(reports.totalPointsIssued - reports.totalPointsRedeemed)}
                    </p>
                    <p className="text-xs text-orange-700 mt-1">
                      Points available for redemption
                    </p>
                  </div>
                </div>

                {/* Liability in Dollars */}
                <div className="p-4 bg-gray-50 rounded-lg border border-gray-200">
                  <div className="flex items-center justify-between">
                    <div>
                      <p className="text-sm font-medium text-gray-700 mb-1">
                        Estimated Liability Value
                      </p>
                      <p className="text-sm text-gray-600">
                        Based on 1 point = $1 redemption value
                      </p>
                    </div>
                    <div className="text-right">
                      <p className="text-3xl font-bold text-orange-600">
                        {formatCurrency(reports.outstandingLiability)}
                      </p>
                      <p className="text-sm text-gray-600 mt-1">
                        {((reports.totalPointsRedeemed / (reports.totalPointsIssued || 1)) * 100).toFixed(1)}% redemption rate
                      </p>
                    </div>
                  </div>
                </div>

                {/* Unclaimed Points Warning */}
                {reports.unclaimedPoints > 0 && (
                  <Alert className="border-yellow-200 bg-yellow-50">
                    <AlertTriangle className="h-4 w-4 text-yellow-600" />
                    <AlertDescription className="text-yellow-800">
                      <strong>{formatNumber(reports.unclaimedPoints)} unclaimed points</strong> from unregistered card purchases.
                      These points will be added to customer accounts when they register.
                    </AlertDescription>
                  </Alert>
                )}
              </div>
            </CardContent>
          </Card>

          {/* Additional Metrics */}
          <div className="grid gap-4 md:grid-cols-2">
            {/* Customer Insights */}
            <Card>
              <CardHeader>
                <CardTitle>Customer Insights</CardTitle>
                <CardDescription>Average metrics per customer</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="flex items-center justify-between">
                  <span className="text-sm text-gray-600">Avg Points per Customer</span>
                  <span className="text-lg font-bold text-indigo-600">
                    {reports.totalCustomers > 0
                      ? formatNumber(Math.round(reports.avgPointsPerCustomer))
                      : '0'}
                  </span>
                </div>
                <Separator />
                <div className="flex items-center justify-between">
                  <span className="text-sm text-gray-600">Avg Liability per Customer</span>
                  <span className="text-lg font-bold text-orange-600">
                    {reports.totalCustomers > 0
                      ? formatCurrency(reports.outstandingLiability / reports.totalCustomers)
                      : '$0.00'}
                  </span>
                </div>
                <Separator />
                <div className="flex items-center justify-between">
                  <span className="text-sm text-gray-600">Total Transactions</span>
                  <span className="text-lg font-bold">
                    {formatNumber(reports.totalTransactions)}
                  </span>
                </div>
              </CardContent>
            </Card>

            {/* Report Period */}
            <Card>
              <CardHeader>
                <div className="flex items-center gap-2">
                  <Calendar className="h-5 w-5 text-indigo-600" />
                  <div>
                    <CardTitle>Report Period</CardTitle>
                    <CardDescription>All-time metrics</CardDescription>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="p-4 bg-indigo-50 rounded-lg border border-indigo-200">
                  <p className="text-sm text-indigo-900 mb-2">
                    This report includes all data from program inception to present.
                  </p>
                  <p className="text-xs text-indigo-700">
                    • Points issued: Lifetime total<br />
                    • Points redeemed: Lifetime total<br />
                    • Outstanding liability: Current snapshot<br />
                    • Unclaimed points: Current pending
                  </p>
                </div>

                {/* Future: Date range selector */}
                <div className="p-4 bg-gray-50 rounded-lg border border-gray-200">
                  <p className="text-sm font-medium text-gray-700 mb-1">Coming Soon</p>
                  <p className="text-xs text-gray-600">
                    • Date range filtering<br />
                    • Monthly/quarterly breakdowns<br />
                    • Trend analysis & charts<br />
                    • Export to CSV/PDF
                  </p>
                </div>
              </CardContent>
            </Card>
          </div>

          {/* Accounting Note */}
          <Card className="border-blue-200 bg-blue-50">
            <CardHeader>
              <CardTitle className="text-blue-900">Accounting Note</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-blue-800">
                <strong>Outstanding liability</strong> represents the potential future cost of points that customers can redeem.
                This should be tracked as a liability on your balance sheet. Consult with your accountant for proper treatment
                of loyalty program liabilities based on your jurisdiction and accounting standards.
              </p>
            </CardContent>
          </Card>
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}

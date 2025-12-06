import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '@/contexts/AuthContext'
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
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Badge } from '@/components/ui/badge'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Loader2,
  Calendar,
  DollarSign,
  CreditCard,
  ChevronDown,
  MoreHorizontal,
  Plus,
  Download,
  TrendingUp,
  AlertCircle,
} from 'lucide-react'

export default function Transactions() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [transactions, setTransactions] = useState([])
  const [loading, setLoading] = useState(true)
  const [activeTab, setActiveTab] = useState('all')
  const [selectedTransactions, setSelectedTransactions] = useState(new Set())

  useEffect(() => {
    document.title = 'Transactions | Lemonade Loyalty'
  }, [])

  useEffect(() => {
    if (!user) {
      navigate('/signin')
      return
    }
    loadTransactions()
  }, [user, navigate])

  const loadTransactions = async () => {
    try {
      setLoading(true)
      const token = (await user.getSession()).access_token
      const response = await fetch('/api/payments', {
        headers: {
          'Authorization': `Bearer ${token}`,
        },
      })

      if (response.ok) {
        const data = await response.json()
        setTransactions(data)
      }
    } catch (error) {
      console.error('Error loading transactions:', error)
    } finally {
      setLoading(false)
    }
  }

  const formatDate = (dateString) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
      hour12: true
    })
  }

  const formatAmount = (amount) => {
    if (!amount) return '$0.00'
    return `$${Number(amount).toFixed(2)}`
  }

  const getStatusBadge = (status) => {
    const statusConfig = {
      completed: { label: 'Succeeded', className: 'bg-green-50 text-green-700 border-green-200' },
      pending: { label: 'Processing', className: 'bg-blue-50 text-blue-700 border-blue-200' },
      failed: { label: 'Failed', className: 'bg-red-50 text-red-700 border-red-200' },
      refunded: { label: 'Refunded', className: 'bg-gray-50 text-gray-700 border-gray-200' },
    }
    const config = statusConfig[status] || statusConfig.completed
    return (
      <Badge variant="outline" className={`${config.className} font-normal`}>
        {config.label}
      </Badge>
    )
  }

  const getFilteredTransactions = () => {
    if (activeTab === 'all') return transactions
    return transactions.filter(t => t.status === activeTab)
  }

  const getStatusCounts = () => {
    const counts = {
      all: transactions.length,
      completed: transactions.filter(t => t.status === 'completed').length,
      refunded: transactions.filter(t => t.status === 'refunded').length,
      failed: transactions.filter(t => t.status === 'failed').length,
    }
    return counts
  }

  const toggleSelectAll = () => {
    const filtered = getFilteredTransactions()
    if (selectedTransactions.size === filtered.length) {
      setSelectedTransactions(new Set())
    } else {
      setSelectedTransactions(new Set(filtered.map(t => t.id)))
    }
  }

  const toggleSelectTransaction = (transactionId) => {
    const newSelected = new Set(selectedTransactions)
    if (newSelected.has(transactionId)) {
      newSelected.delete(transactionId)
    } else {
      newSelected.add(transactionId)
    }
    setSelectedTransactions(newSelected)
  }

  const handleViewCustomer = (customerId) => {
    if (customerId) {
      navigate(`/customers/${customerId}`)
    }
  }

  const counts = getStatusCounts()
  const filteredTransactions = getFilteredTransactions()

  return (
    <SidebarProvider>
      <AppSidebar />
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
                  <BreadcrumbPage>Transactions</BreadcrumbPage>
                </BreadcrumbItem>
              </BreadcrumbList>
            </Breadcrumb>
          </div>
        </header>

        <div className="flex flex-1 flex-col gap-4 p-6">
          {/* Header */}
          <div className="flex items-center justify-between">
            <h1 className="text-3xl font-semibold">Transactions</h1>
            <div className="flex items-center gap-2">
              <Button variant="outline" size="sm" className="gap-2">
                <TrendingUp className="h-4 w-4" />
                Analyze
              </Button>
              <Button size="sm" className="gap-2">
                <Plus className="h-4 w-4" />
                Create payment
              </Button>
            </div>
          </div>

          {/* Tabs */}
          <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
            <div className="flex items-center justify-between border-b">
              <TabsList className="h-auto p-0 bg-transparent">
                <TabsTrigger
                  value="all"
                  className="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent px-4 py-3"
                >
                  All
                  <span className="ml-2 text-sm font-semibold">{counts.all.toLocaleString()}+</span>
                </TabsTrigger>
                <TabsTrigger
                  value="completed"
                  className="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent px-4 py-3"
                >
                  Succeeded
                  <span className="ml-2 text-sm font-semibold">{counts.completed.toLocaleString()}+</span>
                </TabsTrigger>
                <TabsTrigger
                  value="refunded"
                  className="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent px-4 py-3"
                >
                  Refunded
                  <span className="ml-2 text-sm">{counts.refunded}</span>
                </TabsTrigger>
                <TabsTrigger
                  value="failed"
                  className="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent px-4 py-3"
                >
                  Failed
                  <span className="ml-2 text-sm">{counts.failed}</span>
                </TabsTrigger>
              </TabsList>
            </div>

            {/* Filters */}
            <div className="flex items-center gap-2 py-4">
              <Button variant="outline" size="sm" className="gap-2 h-8">
                <Calendar className="h-3.5 w-3.5" />
                Date and time
                <ChevronDown className="h-3 w-3 opacity-50" />
              </Button>
              <Button variant="outline" size="sm" className="gap-2 h-8">
                <DollarSign className="h-3.5 w-3.5" />
                Amount
                <ChevronDown className="h-3 w-3 opacity-50" />
              </Button>
              <Button variant="outline" size="sm" className="gap-2 h-8">
                <CreditCard className="h-3.5 w-3.5" />
                Payment method
                <ChevronDown className="h-3 w-3 opacity-50" />
              </Button>
              <Button variant="outline" size="sm" className="gap-2 h-8">
                <AlertCircle className="h-3.5 w-3.5" />
                Status
                <ChevronDown className="h-3 w-3 opacity-50" />
              </Button>
              <div className="flex-1"></div>
              <Button variant="outline" size="sm" className="gap-2 h-8">
                <Download className="h-3.5 w-3.5" />
                Export
              </Button>
              <Button variant="outline" size="sm" className="gap-2 h-8">
                Edit columns
              </Button>
            </div>

            {/* Table */}
            <TabsContent value={activeTab} className="mt-0">
              {loading ? (
                <div className="flex items-center justify-center py-12 border rounded-lg">
                  <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
                </div>
              ) : filteredTransactions.length === 0 ? (
                <div className="text-center py-12 border rounded-lg">
                  <p className="text-lg font-medium mb-2">No transactions yet</p>
                  <p className="text-sm text-muted-foreground">
                    Transactions will appear here when customers make payments
                  </p>
                </div>
              ) : (
                <>
                  <div className="border rounded-lg">
                    <Table>
                      <TableHeader>
                        <TableRow className="hover:bg-transparent">
                          <TableHead className="w-12">
                            <Checkbox
                              checked={selectedTransactions.size === filteredTransactions.length && filteredTransactions.length > 0}
                              onCheckedChange={toggleSelectAll}
                            />
                          </TableHead>
                          <TableHead className="font-medium">Amount</TableHead>
                          <TableHead className="font-medium">Payment method</TableHead>
                          <TableHead className="font-medium">Description</TableHead>
                          <TableHead className="font-medium">Customer</TableHead>
                          <TableHead className="font-medium">Date</TableHead>
                          <TableHead className="w-12"></TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {filteredTransactions.map((transaction) => (
                          <TableRow
                            key={transaction.id}
                            className="cursor-pointer"
                          >
                            <TableCell onClick={(e) => e.stopPropagation()}>
                              <Checkbox
                                checked={selectedTransactions.has(transaction.id)}
                                onCheckedChange={() => toggleSelectTransaction(transaction.id)}
                              />
                            </TableCell>
                            <TableCell>
                              <div className="flex items-center gap-3">
                                <div className="font-medium">{formatAmount(transaction.amount)}</div>
                                <div className="text-xs text-muted-foreground">NZD</div>
                                {getStatusBadge(transaction.status)}
                              </div>
                            </TableCell>
                            <TableCell>
                              <div className="flex items-center gap-2">
                                <div className="flex items-center gap-1.5 text-sm">
                                  <CreditCard className="h-3.5 w-3.5" />
                                  •••• {transaction.stripePaymentIntentId?.slice(-4) || '0000'}
                                </div>
                              </div>
                            </TableCell>
                            <TableCell className="max-w-md">
                              <div className="truncate text-sm">{transaction.description}</div>
                            </TableCell>
                            <TableCell>
                              {transaction.customer ? (
                                <button
                                  onClick={() => handleViewCustomer(transaction.customerId)}
                                  className="text-sm hover:underline text-left"
                                >
                                  {transaction.customer.email || transaction.customer.name}
                                </button>
                              ) : (
                                <span className="text-sm text-muted-foreground">—</span>
                              )}
                            </TableCell>
                            <TableCell className="text-sm text-muted-foreground">
                              {formatDate(transaction.createdAt)}
                            </TableCell>
                            <TableCell onClick={(e) => e.stopPropagation()}>
                              <DropdownMenu>
                                <DropdownMenuTrigger asChild>
                                  <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
                                    <MoreHorizontal className="h-4 w-4" />
                                  </Button>
                                </DropdownMenuTrigger>
                                <DropdownMenuContent align="end">
                                  <DropdownMenuItem>View details</DropdownMenuItem>
                                  {transaction.customerId && (
                                    <DropdownMenuItem onClick={() => handleViewCustomer(transaction.customerId)}>
                                      View customer
                                    </DropdownMenuItem>
                                  )}
                                  <DropdownMenuSeparator />
                                  <DropdownMenuItem className="text-destructive">
                                    Refund transaction
                                  </DropdownMenuItem>
                                </DropdownMenuContent>
                              </DropdownMenu>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>

                  {/* Footer */}
                  <div className="flex items-center justify-between px-2 py-4 text-sm text-muted-foreground">
                    <div>
                      Viewing <span className="font-medium">1-{Math.min(20, filteredTransactions.length)}</span> of over{' '}
                      <span className="font-medium">{filteredTransactions.length.toLocaleString()}</span> results
                    </div>
                    <div className="flex items-center gap-2">
                      <Button variant="outline" size="sm" disabled>
                        Previous
                      </Button>
                      <Button variant="outline" size="sm">
                        Next
                      </Button>
                    </div>
                  </div>
                </>
              )}
            </TabsContent>
          </Tabs>
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}

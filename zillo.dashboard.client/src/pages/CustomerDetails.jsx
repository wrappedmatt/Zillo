import { useState, useEffect } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
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
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Alert, AlertDescription } from '@/components/ui/alert'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import {
  Loader2,
  ArrowLeft,
  CreditCard,
  TrendingUp,
  TrendingDown,
  Gift,
  Calendar,
  Mail,
  Phone,
  User,
  Link as LinkIcon,
  Smartphone,
  RefreshCw,
  DollarSign,
  Bell
} from 'lucide-react'

export default function CustomerDetails() {
  const { customerId } = useParams()
  const { user } = useAuth()
  const navigate = useNavigate()
  const [customer, setCustomer] = useState(null)
  const [cards, setCards] = useState([])
  const [transactions, setTransactions] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [account, setAccount] = useState(null)
  const [adjustmentDialogOpen, setAdjustmentDialogOpen] = useState(false)
  const [adjustmentAmount, setAdjustmentAmount] = useState('')
  const [adjustmentDescription, setAdjustmentDescription] = useState('')
  const [adjustmentSubmitting, setAdjustmentSubmitting] = useState(false)
  const [notificationDialogOpen, setNotificationDialogOpen] = useState(false)
  const [notificationMessage, setNotificationMessage] = useState('')
  const [notificationSubmitting, setNotificationSubmitting] = useState(false)

  useEffect(() => {
    if (customer) {
      document.title = `${customer.name} | Zillo Loyalty`
    } else {
      document.title = 'Customer Details | Zillo Loyalty'
    }
  }, [customer])

  useEffect(() => {
    if (!user) {
      navigate('/signin')
      return
    }
    loadCustomerDetails()
  }, [user, customerId, navigate])

  const loadCustomerDetails = async () => {
    try {
      setLoading(true)
      setError('')
      const { data: { session } } = await supabase.auth.getSession()

      if (!session) {
        navigate('/signin')
        return
      }

      // Load account configuration
      const accountResponse = await fetch('/api/accounts/me', {
        headers: {
          'Authorization': `Bearer ${session.access_token}`
        }
      })

      if (accountResponse.ok) {
        const accountData = await accountResponse.json()
        setAccount(accountData)
      }

      // Load customer data
      const customerResponse = await fetch(`/api/customers/${customerId}`, {
        headers: {
          'Authorization': `Bearer ${session.access_token}`
        }
      })

      if (!customerResponse.ok) {
        throw new Error('Failed to load customer')
      }

      const customerData = await customerResponse.json()
      setCustomer(customerData)

      // Load customer cards
      const cardsResponse = await fetch(`/api/customers/${customerId}/cards`, {
        headers: {
          'Authorization': `Bearer ${session.access_token}`
        }
      })

      if (cardsResponse.ok) {
        const cardsData = await cardsResponse.json()
        setCards(cardsData)
      }

      // Load customer transactions
      const transactionsResponse = await fetch(`/api/customers/${customerId}/transactions`, {
        headers: {
          'Authorization': `Bearer ${session.access_token}`
        }
      })

      if (transactionsResponse.ok) {
        const transactionsData = await transactionsResponse.json()
        setTransactions(transactionsData)
      }
    } catch (error) {
      console.error('Error loading customer details:', error)
      setError(error.message)
    } finally {
      setLoading(false)
    }
  }

  const formatDate = (dateString) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    })
  }

  const getTransactionIcon = (type) => {
    switch (type) {
      case 'earn':
      case 'cashback_earn':
        return <TrendingUp className="h-4 w-4 text-green-600" />
      case 'redeem':
      case 'cashback_redeem':
        return <TrendingDown className="h-4 w-4 text-red-600" />
      case 'bonus':
      case 'welcome_bonus':
        return <Gift className="h-4 w-4 text-indigo-600" />
      default:
        return <Calendar className="h-4 w-4 text-gray-600" />
    }
  }

  const getTransactionColor = (type) => {
    switch (type) {
      case 'earn':
      case 'cashback_earn':
        return 'text-green-600'
      case 'redeem':
      case 'cashback_redeem':
        return 'text-red-600'
      case 'bonus':
      case 'welcome_bonus':
        return 'text-indigo-600'
      default:
        return 'text-gray-600'
    }
  }

  const getTransactionLabel = (type) => {
    switch (type) {
      case 'cashback_earn':
        return 'Cashback Earned'
      case 'cashback_redeem':
        return 'Cashback Redeemed'
      case 'welcome_bonus':
        return 'Welcome Bonus'
      case 'earn':
        return 'Points Earned'
      case 'redeem':
        return 'Points Redeemed'
      case 'adjustment':
        return 'Adjustment'
      default:
        return type
    }
  }

  const generatePortalLink = async () => {
    try {
      const { data: { session } } = await supabase.auth.getSession()

      const response = await fetch(`/api/customers/${customerId}/portal-token`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${session.access_token}`
        }
      })

      if (response.ok) {
        const data = await response.json()
        const portalUrl = `${window.location.origin}/portal/${data.token}`

        // Copy to clipboard
        await navigator.clipboard.writeText(portalUrl)
        alert('Portal link copied to clipboard!')
      }
    } catch (error) {
      console.error('Error generating portal link:', error)
      alert('Failed to generate portal link')
    }
  }

  const updateWalletPass = async () => {
    try {
      const { data: { session } } = await supabase.auth.getSession()

      const response = await fetch(`/api/wallet/update-pass/${customerId}`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${session.access_token}`
        }
      })

      if (response.ok) {
        alert('Wallet pass updated successfully!')
      } else {
        alert('Failed to update wallet pass')
      }
    } catch (error) {
      console.error('Error updating wallet pass:', error)
      alert('Failed to update wallet pass')
    }
  }

  const handleAdjustment = async () => {
    if (!adjustmentAmount || !adjustmentDescription) {
      alert('Please enter both amount and description')
      return
    }

    const amount = parseFloat(adjustmentAmount)
    if (isNaN(amount)) {
      alert('Please enter a valid number')
      return
    }

    try {
      setAdjustmentSubmitting(true)
      const { data: { session } } = await supabase.auth.getSession()

      const requestBody = {
        description: adjustmentDescription
      }

      // Add either points or cashback based on account type
      if (account?.loyaltySystemType === 'cashback') {
        requestBody.cashbackAmount = Math.round(amount * 100) // Convert dollars to cents
      } else {
        requestBody.points = Math.round(amount) // Points are whole numbers
      }

      const response = await fetch(`/api/customers/${customerId}/adjustment`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${session.access_token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(requestBody)
      })

      if (response.ok) {
        alert('Credit adjustment added successfully!')
        setAdjustmentDialogOpen(false)
        setAdjustmentAmount('')
        setAdjustmentDescription('')
        // Reload customer data to show updated balance
        loadCustomerDetails()
      } else {
        const error = await response.json()
        alert(`Failed to add adjustment: ${error.error || 'Unknown error'}`)
      }
    } catch (error) {
      console.error('Error adding adjustment:', error)
      alert('Failed to add adjustment')
    } finally {
      setAdjustmentSubmitting(false)
    }
  }

  const handleSendNotification = async () => {
    if (!notificationMessage) {
      alert('Please enter a message')
      return
    }

    try {
      setNotificationSubmitting(true)
      const { data: { session } } = await supabase.auth.getSession()

      const response = await fetch(`/api/customers/${customerId}/send-notification`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${session.access_token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ message: notificationMessage })
      })

      if (response.ok) {
        alert('Notification sent successfully!')
        setNotificationDialogOpen(false)
        setNotificationMessage('')
      } else {
        const error = await response.json()
        alert(`Failed to send notification: ${error.error || 'Unknown error'}`)
      }
    } catch (error) {
      console.error('Error sending notification:', error)
      alert('Failed to send notification')
    } finally {
      setNotificationSubmitting(false)
    }
  }

  if (loading) {
    return (
      <SidebarProvider>
        <AppSidebar />
        <SidebarInset>
          <div className="flex items-center justify-center h-screen">
            <Loader2 className="h-8 w-8 animate-spin text-indigo-600" />
          </div>
        </SidebarInset>
      </SidebarProvider>
    )
  }

  if (error || !customer) {
    return (
      <SidebarProvider>
        <AppSidebar />
        <SidebarInset>
          <div className="flex items-center justify-center h-screen p-4">
            <Card className="w-full max-w-md">
              <CardHeader>
                <CardTitle className="text-red-600">Error</CardTitle>
              </CardHeader>
              <CardContent>
                <Alert variant="destructive">
                  <AlertDescription>{error || 'Customer not found'}</AlertDescription>
                </Alert>
                <Button onClick={() => navigate('/customers')} className="mt-4 w-full">
                  <ArrowLeft className="h-4 w-4 mr-2" />
                  Back to Customers
                </Button>
              </CardContent>
            </Card>
          </div>
        </SidebarInset>
      </SidebarProvider>
    )
  }

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
                  <BreadcrumbLink href="/customers">
                    Customers
                  </BreadcrumbLink>
                </BreadcrumbItem>
                <BreadcrumbSeparator className="hidden md:block" />
                <BreadcrumbItem>
                  <BreadcrumbPage>{customer.name}</BreadcrumbPage>
                </BreadcrumbItem>
              </BreadcrumbList>
            </Breadcrumb>
          </div>
        </header>

        <div className="flex flex-1 flex-col gap-4 p-4 pt-0">
          {/* Back Button */}
          <Button
            variant="outline"
            onClick={() => navigate('/customers')}
            className="w-fit"
          >
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back to Customers
          </Button>

          {/* Customer Header */}
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-4">
                  <div className="flex h-16 w-16 items-center justify-center rounded-full bg-indigo-100">
                    <span className="text-indigo-600 font-bold text-2xl">
                      {customer.name.charAt(0).toUpperCase()}
                    </span>
                  </div>
                  <div>
                    <CardTitle className="text-2xl">{customer.name}</CardTitle>
                    <CardDescription>
                      Member since {formatDate(customer.createdAt)}
                    </CardDescription>
                  </div>
                </div>
                <div className="flex gap-2">
                  <Button onClick={() => setAdjustmentDialogOpen(true)} variant="outline">
                    <DollarSign className="h-4 w-4 mr-2" />
                    Give Credit
                  </Button>
                  <Button onClick={() => setNotificationDialogOpen(true)} variant="outline">
                    <Bell className="h-4 w-4 mr-2" />
                    Send Notification
                  </Button>
                  <Button onClick={updateWalletPass} variant="outline">
                    <Smartphone className="h-4 w-4 mr-2" />
                    Update Wallet Pass
                  </Button>
                  <Button onClick={generatePortalLink} variant="outline">
                    <LinkIcon className="h-4 w-4 mr-2" />
                    Copy Portal Link
                  </Button>
                </div>
              </div>
            </CardHeader>
          </Card>

          {/* Balance Card */}
          <Card className="bg-gradient-to-r from-indigo-600 to-purple-600 border-0">
            <CardContent className="pt-6">
              <div className="text-center text-white">
                {account?.loyaltySystemType === 'cashback' ? (
                  <>
                    <p className="text-sm font-medium mb-1 opacity-90">Credit Available</p>
                    <p className="text-6xl font-bold mb-2">${((customer.cashbackBalance || 0) / 100).toFixed(2)}</p>
                    <p className="text-sm opacity-90">available to use</p>
                  </>
                ) : (
                  <>
                    <p className="text-sm font-medium mb-1 opacity-90">Points Balance</p>
                    <p className="text-6xl font-bold mb-2">{customer.pointsBalance}</p>
                    <p className="text-sm opacity-90">points available</p>
                  </>
                )}
              </div>
            </CardContent>
          </Card>

          <div className="grid gap-6 md:grid-cols-2">
            {/* Contact Information */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <User className="h-5 w-5" />
                  Contact Information
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div>
                  <p className="text-sm font-medium text-gray-600">Name</p>
                  <p className="text-lg font-medium">{customer.name}</p>
                </div>
                {customer.email ? (
                  <div>
                    <p className="text-sm font-medium text-gray-600 mb-1">Email</p>
                    <div className="flex items-center gap-2">
                      <Mail className="h-4 w-4 text-gray-500" />
                      <p className="text-lg">{customer.email}</p>
                    </div>
                  </div>
                ) : (
                  <div>
                    <p className="text-sm font-medium text-gray-600">Email</p>
                    <p className="text-sm text-gray-400 italic">Not provided</p>
                  </div>
                )}
                {customer.phoneNumber ? (
                  <div>
                    <p className="text-sm font-medium text-gray-600 mb-1">Phone</p>
                    <div className="flex items-center gap-2">
                      <Phone className="h-4 w-4 text-gray-500" />
                      <p className="text-lg">{customer.phoneNumber}</p>
                    </div>
                  </div>
                ) : (
                  <div>
                    <p className="text-sm font-medium text-gray-600">Phone</p>
                    <p className="text-sm text-gray-400 italic">Not provided</p>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Registered Cards */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <CreditCard className="h-5 w-5" />
                  Registered Cards
                </CardTitle>
                <CardDescription>
                  {cards.length} {cards.length === 1 ? 'card' : 'cards'} linked to account
                </CardDescription>
              </CardHeader>
              <CardContent>
                {cards.length > 0 ? (
                  <div className="space-y-3">
                    {cards.map((card) => (
                      <div
                        key={card.id}
                        className="flex items-center justify-between p-4 border rounded-lg bg-gray-50"
                      >
                        <div className="flex items-center gap-3">
                          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-indigo-100">
                            <CreditCard className="h-5 w-5 text-indigo-600" />
                          </div>
                          <div>
                            <p className="font-medium capitalize">
                              {card.cardBrand || 'Card'} •••• {card.cardLast4 || '****'}
                            </p>
                            <p className="text-sm text-gray-600">
                              Last used: {formatDate(card.lastUsedAt)}
                            </p>
                          </div>
                        </div>
                        {card.isPrimary && (
                          <Badge variant="secondary">Primary</Badge>
                        )}
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-gray-600 text-center py-4">No cards registered</p>
                )}
              </CardContent>
            </Card>
          </div>

          {/* Transaction History */}
          <Card>
            <CardHeader>
              <CardTitle>Transaction History</CardTitle>
              <CardDescription>
                {transactions.length} {transactions.length === 1 ? 'transaction' : 'transactions'}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {transactions.length > 0 ? (
                <div className="space-y-1">
                  {transactions.map((transaction, index) => (
                    <div key={transaction.id}>
                      {index > 0 && <Separator className="my-2" />}
                      <div className="flex items-center justify-between py-3">
                        <div className="flex items-center gap-3">
                          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-gray-100">
                            {getTransactionIcon(transaction.type)}
                          </div>
                          <div>
                            <p className="font-medium">{getTransactionLabel(transaction.type)}</p>
                            <p className="text-sm text-gray-600">
                              {formatDate(transaction.createdAt)}
                            </p>
                          </div>
                        </div>
                        <div className="text-right">
                          {account?.loyaltySystemType === 'cashback' ? (
                            <>
                              <p className={`text-lg font-bold ${getTransactionColor(transaction.type)}`}>
                                {(transaction.type === 'cashback_earn' || transaction.type === 'welcome_bonus') ? '+' : ''}
                                ${(Math.abs(transaction.cashbackAmount || 0) / 100).toFixed(2)}
                              </p>
                              {transaction.amount && (
                                <p className="text-sm text-gray-600">
                                  Purchase: ${(Math.abs(transaction.amount) / 100).toFixed(2)}
                                </p>
                              )}
                            </>
                          ) : (
                            <>
                              <p className={`text-lg font-bold ${getTransactionColor(transaction.type)}`}>
                                {transaction.points > 0 ? '+' : ''}{transaction.points}
                              </p>
                              {transaction.amount && (
                                <p className="text-sm text-gray-600">
                                  ${(Math.abs(transaction.amount) / 100).toFixed(2)}
                                </p>
                              )}
                            </>
                          )}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-gray-600 text-center py-8">No transactions yet</p>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Credit Adjustment Dialog */}
        <Dialog open={adjustmentDialogOpen} onOpenChange={setAdjustmentDialogOpen}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Give Credit Adjustment</DialogTitle>
              <DialogDescription>
                Add a manual credit adjustment to this customer's account.
                {account?.loyaltySystemType === 'cashback'
                  ? ' Enter the dollar amount to add or subtract.'
                  : ' Enter the points to add or subtract.'}
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <div className="space-y-2">
                <Label htmlFor="amount">
                  {account?.loyaltySystemType === 'cashback' ? 'Amount ($)' : 'Points'}
                </Label>
                <Input
                  id="amount"
                  type="number"
                  step={account?.loyaltySystemType === 'cashback' ? '0.01' : '1'}
                  placeholder={account?.loyaltySystemType === 'cashback' ? '0.00' : '0'}
                  value={adjustmentAmount}
                  onChange={(e) => setAdjustmentAmount(e.target.value)}
                />
                <p className="text-sm text-gray-500">
                  Use negative numbers to subtract credit
                </p>
              </div>
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  placeholder="Reason for adjustment..."
                  value={adjustmentDescription}
                  onChange={(e) => setAdjustmentDescription(e.target.value)}
                  rows={3}
                />
              </div>
            </div>
            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => setAdjustmentDialogOpen(false)}
                disabled={adjustmentSubmitting}
              >
                Cancel
              </Button>
              <Button
                onClick={handleAdjustment}
                disabled={adjustmentSubmitting}
              >
                {adjustmentSubmitting ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Adding...
                  </>
                ) : (
                  'Add Adjustment'
                )}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Send Notification Dialog */}
        <Dialog open={notificationDialogOpen} onOpenChange={setNotificationDialogOpen}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Send Push Notification</DialogTitle>
              <DialogDescription>
                Send a custom message to this customer's Apple Wallet pass.
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <div className="space-y-2">
                <Label htmlFor="message">Message</Label>
                <Textarea
                  id="message"
                  placeholder="Enter notification message..."
                  value={notificationMessage}
                  onChange={(e) => setNotificationMessage(e.target.value)}
                  rows={3}
                />
              </div>
            </div>
            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => setNotificationDialogOpen(false)}
                disabled={notificationSubmitting}
              >
                Cancel
              </Button>
              <Button
                onClick={handleSendNotification}
                disabled={notificationSubmitting}
              >
                {notificationSubmitting ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Sending...
                  </>
                ) : (
                  'Send Notification'
                )}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </SidebarInset>
    </SidebarProvider>
  )
}

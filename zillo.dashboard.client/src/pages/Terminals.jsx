import { useState, useEffect, useRef } from 'react'
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
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Plus, Smartphone, Copy, Check, XCircle, Trash2, CreditCard, ExternalLink, AlertCircle, CheckCircle2, Loader2 } from 'lucide-react'
import { Alert, AlertDescription } from '@/components/ui/alert'

export default function Terminals() {
  const { user, loading: authLoading } = useAuth()
  const navigate = useNavigate()
  const [terminals, setTerminals] = useState([])
  const [account, setAccount] = useState(null)
  const [loading, setLoading] = useState(true)
  const [showPairingDialog, setShowPairingDialog] = useState(false)
  const [pairingCode, setPairingCode] = useState(null)
  const [pairingExpiry, setPairingExpiry] = useState(null)
  const [copiedCode, setCopiedCode] = useState(false)
  const [stripeLoading, setStripeLoading] = useState(false)
  const [error, setError] = useState('')
  const hasRefreshedStripe = useRef(false)

  useEffect(() => {
    document.title = 'Terminals | Zillo'
  }, [])

  useEffect(() => {
    // Wait for auth to be determined before redirecting
    if (authLoading) return

    if (!user) {
      navigate('/signin')
      return
    }
    loadAccount()
    loadTerminals()
  }, [user, authLoading, navigate])

  // Refresh Stripe status when returning from Stripe onboarding
  useEffect(() => {
    const refreshStripeStatus = async () => {
      // Only refresh once per page load if we have a stripeAccountId
      if (hasRefreshedStripe.current) return
      if (authLoading || !user) return

      try {
        const { data: { session } } = await supabase.auth.getSession()
        if (!session) return

        // Refresh status from Stripe API
        const response = await fetch('/api/stripe-connect/status?refresh=true', {
          headers: {
            'Authorization': `Bearer ${session.access_token}`
          }
        })

        if (response.ok) {
          // Reload account to get updated status
          await loadAccount()
        }
        hasRefreshedStripe.current = true
      } catch (error) {
        console.error('Error refreshing Stripe status:', error)
      }
    }

    // If returning from Stripe (account already has stripeAccountId but status might be stale)
    if (account?.stripeAccountId && !account?.stripeChargesEnabled) {
      refreshStripeStatus()
    }
  }, [account?.stripeAccountId, account?.stripeChargesEnabled, authLoading, user])

  const loadAccount = async () => {
    try {
      const { data: { session } } = await supabase.auth.getSession()
      if (!session) return

      const response = await fetch('/api/accounts/me', {
        headers: {
          'Authorization': `Bearer ${session.access_token}`
        }
      })

      if (response.ok) {
        const data = await response.json()
        setAccount(data)
      }
    } catch (error) {
      console.error('Error loading account:', error)
    }
  }

  const loadTerminals = async () => {
    try {
      const { data: accountData } = await supabase
        .from('accounts')
        .select('id')
        .eq('supabase_user_id', user.id)
        .single()

      if (!accountData) return

      const { data, error } = await supabase
        .from('terminals')
        .select('*')
        .eq('account_id', accountData.id)
        .order('created_at', { ascending: false })

      if (error) throw error
      setTerminals(data || [])
    } catch (error) {
      console.error('Error loading terminals:', error)
    } finally {
      setLoading(false)
    }
  }

  const handleGeneratePairingCode = async () => {
    try {
      const { data: accountData } = await supabase
        .from('accounts')
        .select('id')
        .eq('supabase_user_id', user.id)
        .single()

      if (!accountData) {
        alert('Account not found')
        return
      }

      const { data: { session } } = await supabase.auth.getSession()

      // Store the current terminal count before generating the pairing code
      const initialTerminalCount = terminals.length

      // Call the terminal management API to generate pairing code
      const response = await fetch('/api/TerminalManagement/generate-pairing-code', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Account-Id': accountData.id
        },
        body: JSON.stringify({
          terminalLabel: 'Terminal ' + (terminals.length + 1)
        })
      })

      if (!response.ok) {
        throw new Error('Failed to generate pairing code')
      }

      const data = await response.json()
      setPairingCode(data.pairingCode)
      setPairingExpiry(new Date(data.expiresAt))
      setShowPairingDialog(true)

      // Poll for terminal pairing every 2 seconds to update the list
      const pollInterval = setInterval(async () => {
        // Reload terminals in the background to show the paired terminal
        await loadTerminals()
      }, 2000)

      // Clear polling after 5 minutes (pairing code expiry)
      setTimeout(() => {
        clearInterval(pollInterval)
      }, 5 * 60 * 1000)

    } catch (error) {
      console.error('Error generating pairing code:', error)
      alert('Failed to generate pairing code: ' + error.message)
    }
  }

  const checkNewTerminals = async () => {
    try {
      const { data: accountData } = await supabase
        .from('accounts')
        .select('id')
        .eq('supabase_user_id', user.id)
        .single()

      if (!accountData) return 0

      const { data, error } = await supabase
        .from('terminals')
        .select('id')
        .eq('account_id', accountData.id)

      if (error) return 0
      return data?.length || 0
    } catch (error) {
      return 0
    }
  }

  const handleCopyPairingCode = () => {
    if (pairingCode) {
      navigator.clipboard.writeText(pairingCode)
      setCopiedCode(true)
      setTimeout(() => setCopiedCode(false), 2000)
    }
  }

  const handleRevokeTerminal = async (terminalId) => {
    if (!confirm('Are you sure you want to revoke this terminal? It will no longer be able to process transactions.')) {
      return
    }

    try {
      const { data: accountData } = await supabase
        .from('accounts')
        .select('id')
        .eq('supabase_user_id', user.id)
        .single()

      const response = await fetch(`/api/TerminalManagement/${terminalId}/revoke`, {
        method: 'POST',
        headers: {
          'X-Account-Id': accountData.id
        }
      })

      if (!response.ok) {
        throw new Error('Failed to revoke terminal')
      }

      loadTerminals()
    } catch (error) {
      console.error('Error revoking terminal:', error)
      alert('Failed to revoke terminal: ' + error.message)
    }
  }

  const handleDeleteTerminal = async (terminalId) => {
    if (!confirm('Are you sure you want to delete this terminal permanently? This action cannot be undone.')) {
      return
    }

    try {
      const { data: accountData } = await supabase
        .from('accounts')
        .select('id')
        .eq('supabase_user_id', user.id)
        .single()

      const response = await fetch(`/api/TerminalManagement/${terminalId}`, {
        method: 'DELETE',
        headers: {
          'X-Account-Id': accountData.id
        }
      })

      if (!response.ok) {
        throw new Error('Failed to delete terminal')
      }

      loadTerminals()
    } catch (error) {
      console.error('Error deleting terminal:', error)
      alert('Failed to delete terminal: ' + error.message)
    }
  }

  const handleStripeConnect = async () => {
    setStripeLoading(true)
    setError('')

    try {
      const { data: { session } } = await supabase.auth.getSession()

      // First, create the connected account if not exists
      if (!account?.stripeAccountId) {
        const createResponse = await fetch('/api/stripe-connect/create-account', {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${session.access_token}`,
            'Content-Type': 'application/json'
          }
        })

        if (!createResponse.ok) {
          const data = await createResponse.json()
          throw new Error(data.error || 'Failed to create Stripe account')
        }
      }

      // Get onboarding link
      const linkResponse = await fetch('/api/stripe-connect/onboarding-link', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${session.access_token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          returnUrl: `${window.location.origin}/terminals`,
          refreshUrl: `${window.location.origin}/terminals`
        })
      })

      if (!linkResponse.ok) {
        const data = await linkResponse.json()
        throw new Error(data.error || 'Failed to create onboarding link')
      }

      const { url } = await linkResponse.json()
      window.location.href = url
    } catch (error) {
      console.error('Error connecting to Stripe:', error)
      setError(error.message)
    } finally {
      setStripeLoading(false)
    }
  }

  const handleStripeDashboard = async () => {
    setStripeLoading(true)
    setError('')

    try {
      const { data: { session } } = await supabase.auth.getSession()

      const response = await fetch('/api/stripe-connect/dashboard-link', {
        headers: {
          'Authorization': `Bearer ${session.access_token}`
        }
      })

      if (!response.ok) {
        const data = await response.json()
        throw new Error(data.error || 'Failed to get dashboard link')
      }

      const { url } = await response.json()
      window.open(url, '_blank')
    } catch (error) {
      console.error('Error opening Stripe dashboard:', error)
      setError(error.message)
    } finally {
      setStripeLoading(false)
    }
  }

  const getStripeStatusDisplay = () => {
    const status = account?.stripeOnboardingStatus || 'not_started'
    const chargesEnabled = account?.stripeChargesEnabled

    if (status === 'complete' && chargesEnabled) {
      return {
        label: 'Connected',
        icon: CheckCircle2,
        color: 'text-green-600',
        bgColor: 'bg-green-50',
        borderColor: 'border-green-200'
      }
    } else if (status === 'pending' || (status === 'complete' && !chargesEnabled)) {
      return {
        label: 'Pending verification',
        icon: AlertCircle,
        color: 'text-yellow-600',
        bgColor: 'bg-yellow-50',
        borderColor: 'border-yellow-200'
      }
    } else if (status === 'restricted') {
      return {
        label: 'Action required',
        icon: AlertCircle,
        color: 'text-orange-600',
        bgColor: 'bg-orange-50',
        borderColor: 'border-orange-200'
      }
    } else {
      return {
        label: 'Not connected',
        icon: XCircle,
        color: 'text-muted-foreground',
        bgColor: 'bg-muted/30',
        borderColor: 'border-border'
      }
    }
  }

  const getStatusBadge = (terminal) => {
    // Check if terminal is pending pairing (never been paired)
    if (!terminal.paired_at) {
      return <Badge variant="secondary">Pending Pairing</Badge>
    }

    // Check if terminal was paired but is now revoked
    if (!terminal.is_active) {
      return <Badge variant="destructive">Revoked</Badge>
    }

    // Terminal is active and has been paired - check connection status
    const now = new Date()
    const lastSeen = terminal.last_seen_at ? new Date(terminal.last_seen_at) : null

    if (!lastSeen) {
      return <Badge variant="secondary">Never Connected</Badge>
    }

    const timeSinceLastSeen = now - lastSeen
    const minutesSinceLastSeen = timeSinceLastSeen / (1000 * 60)

    if (minutesSinceLastSeen < 5) {
      return <Badge variant="default" className="bg-green-500">Online</Badge>
    } else if (minutesSinceLastSeen < 60) {
      return <Badge variant="secondary">Idle</Badge>
    } else {
      return <Badge variant="outline">Offline</Badge>
    }
  }

  const formatDate = (dateString) => {
    if (!dateString) return 'Never'
    return new Date(dateString).toLocaleString()
  }

  if (authLoading || loading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    )
  }

  return (
    <SidebarProvider>
      <AppSidebar account={account} />
      <SidebarInset>
        <header className="flex h-16 shrink-0 items-center gap-2 border-b px-4">
          <SidebarTrigger className="-ml-1" />
          <Separator orientation="vertical" className="mr-2 h-4" />
          <Breadcrumb>
            <BreadcrumbList>
              <BreadcrumbItem className="hidden md:block">
                <BreadcrumbLink href="#">
                  Zillo
                </BreadcrumbLink>
              </BreadcrumbItem>
              <BreadcrumbSeparator className="hidden md:block" />
              <BreadcrumbItem>
                <BreadcrumbPage>Terminals</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
        </header>

        <div className="flex flex-1 flex-col gap-4 p-4 md:p-6">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-xl font-semibold text-foreground">Terminals</h2>
              <p className="text-sm text-muted-foreground">Manage your POS terminals and devices</p>
            </div>
            <Button onClick={handleGeneratePairingCode} disabled={!account?.stripeChargesEnabled}>
              <Plus className="mr-2 h-4 w-4" />
              Add Terminal
            </Button>
          </div>

          {/* Error Alert */}
          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {/* Stripe Connect Section */}
          {(() => {
            const stripeStatus = getStripeStatusDisplay()
            const StatusIcon = stripeStatus.icon
            return (
              <Card className={`${stripeStatus.borderColor} border`}>
                <CardHeader className={stripeStatus.bgColor}>
                  <div className="flex items-center gap-2">
                    <CreditCard className="h-5 w-5" />
                    <CardTitle className="text-lg">Payment Setup</CardTitle>
                  </div>
                  <CardDescription>
                    Connect your Stripe account to accept payments from terminals
                  </CardDescription>
                </CardHeader>
                <CardContent className="pt-6">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <div className={`flex items-center gap-2 px-3 py-1.5 rounded-full ${stripeStatus.bgColor}`}>
                        <StatusIcon className={`h-4 w-4 ${stripeStatus.color}`} />
                        <span className={`text-sm font-medium ${stripeStatus.color}`}>
                          {stripeStatus.label}
                        </span>
                      </div>
                      {account?.stripeChargesEnabled && (
                        <span className="text-sm text-muted-foreground">
                          Ready to accept payments
                        </span>
                      )}
                    </div>
                    <div className="flex items-center gap-2">
                      {account?.stripeAccountId && account?.stripeOnboardingStatus === 'complete' && account?.stripeChargesEnabled ? (
                        <Button
                          variant="outline"
                          onClick={handleStripeDashboard}
                          disabled={stripeLoading}
                        >
                          {stripeLoading ? (
                            <Loader2 className="h-4 w-4 animate-spin mr-2" />
                          ) : (
                            <ExternalLink className="h-4 w-4 mr-2" />
                          )}
                          Stripe Dashboard
                        </Button>
                      ) : (
                        <Button
                          onClick={handleStripeConnect}
                          disabled={stripeLoading}
                        >
                          {stripeLoading ? (
                            <Loader2 className="h-4 w-4 animate-spin mr-2" />
                          ) : (
                            <CreditCard className="h-4 w-4 mr-2" />
                          )}
                          {account?.stripeAccountId ? 'Continue setup' : 'Connect Stripe'}
                        </Button>
                      )}
                    </div>
                  </div>
                  {!account?.stripeChargesEnabled && (
                    <p className="text-sm text-muted-foreground mt-4">
                      You need to connect your Stripe account before you can pair terminals and accept payments.
                    </p>
                  )}
                </CardContent>
              </Card>
            )
          })()}

          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {terminals.map((terminal) => (
              <Card key={terminal.id} className="hover:shadow-md transition-shadow">
                <CardHeader>
                  <div className="flex items-start justify-between">
                    <div className="flex items-center gap-2">
                      <Smartphone className="h-5 w-5 text-muted-foreground" />
                      <div>
                        <CardTitle className="text-lg">{terminal.terminal_label}</CardTitle>
                        {terminal.device_model && (
                          <CardDescription className="text-xs mt-1">
                            {terminal.device_model}
                          </CardDescription>
                        )}
                      </div>
                    </div>
                    {getStatusBadge(terminal)}
                  </div>
                </CardHeader>
                <CardContent>
                  <div className="space-y-2 text-sm">
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">Paired:</span>
                      <span className="font-medium">{formatDate(terminal.paired_at)}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">Last Seen:</span>
                      <span className="font-medium">{formatDate(terminal.last_seen_at)}</span>
                    </div>
                    {terminal.device_id && (
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Device ID:</span>
                        <span className="font-mono text-xs">{terminal.device_id.slice(0, 8)}...</span>
                      </div>
                    )}
                    {!terminal.paired_at && terminal.pairing_code && (
                      <div className="pt-2 pb-2 border-t border-b">
                        <div className="flex items-center justify-between">
                          <span className="text-sm font-medium text-muted-foreground">Pairing Code:</span>
                          <div className="flex items-center gap-2">
                            <span className="text-lg font-mono font-bold">{terminal.pairing_code}</span>
                            <Button
                              variant="ghost"
                              size="icon"
                              className="h-6 w-6"
                              onClick={() => {
                                navigator.clipboard.writeText(terminal.pairing_code)
                                // Could add a toast notification here
                              }}
                            >
                              <Copy className="h-3 w-3" />
                            </Button>
                          </div>
                        </div>
                        {terminal.pairing_expires_at && (
                          <div className="text-xs text-muted-foreground mt-1 text-right">
                            Expires: {formatDate(terminal.pairing_expires_at)}
                          </div>
                        )}
                      </div>
                    )}
                    <div className="pt-2 space-y-2">
                      {terminal.paired_at && terminal.is_active ? (
                        // Active and paired terminal - show revoke button
                        <Button
                          variant="destructive"
                          size="sm"
                          className="w-full"
                          onClick={() => handleRevokeTerminal(terminal.id)}
                        >
                          <XCircle className="mr-2 h-3 w-3" />
                          Revoke Terminal
                        </Button>
                      ) : (
                        // Pending pairing or revoked terminal - show delete button
                        <Button
                          variant="outline"
                          size="sm"
                          className="w-full"
                          onClick={() => handleDeleteTerminal(terminal.id)}
                        >
                          <Trash2 className="mr-2 h-3 w-3" />
                          Delete Terminal
                        </Button>
                      )}
                    </div>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>

          {terminals.length === 0 && (
            <Card>
              <CardContent className="py-12 text-center">
                <Smartphone className="mx-auto h-12 w-12 text-muted-foreground" />
                <h3 className="mt-4 text-lg font-semibold text-card-foreground">No terminals yet</h3>
                <p className="mt-2 text-sm text-muted-foreground">
                  Get started by adding your first terminal device.
                </p>
              </CardContent>
            </Card>
          )}
        </div>
      </SidebarInset>

      <Dialog open={showPairingDialog} onOpenChange={setShowPairingDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Terminal Pairing Code</DialogTitle>
            <DialogDescription>
              Enter this code on your terminal device to pair it with your account
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="flex items-center justify-center gap-2 p-6 bg-muted rounded-lg">
              <span className="text-4xl font-mono font-bold tracking-wider">
                {pairingCode}
              </span>
              <Button
                variant="ghost"
                size="icon"
                onClick={handleCopyPairingCode}
                className="ml-2"
              >
                {copiedCode ? (
                  <Check className="h-4 w-4 text-green-500" />
                ) : (
                  <Copy className="h-4 w-4" />
                )}
              </Button>
            </div>

            {pairingExpiry && (
              <p className="text-sm text-center text-muted-foreground">
                Code expires at {pairingExpiry.toLocaleTimeString()} ({Math.floor((pairingExpiry - new Date()) / 60000)} minutes remaining)
              </p>
            )}

            <div className="text-sm text-muted-foreground space-y-2">
              <p className="font-medium">Instructions:</p>
              <ol className="list-decimal list-inside space-y-1 ml-2">
                <li>Open the Lemonade Terminal app on your device</li>
                <li>Tap "Pair New Terminal"</li>
                <li>Enter the pairing code shown above</li>
                <li>Wait for confirmation</li>
              </ol>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </SidebarProvider>
  )
}

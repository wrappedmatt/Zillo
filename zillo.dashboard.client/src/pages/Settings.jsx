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
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Loader2, CheckCircle2, Link as LinkIcon, Copy, Check, Smartphone } from 'lucide-react'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'

export default function Settings() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [account, setAccount] = useState(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [success, setSuccess] = useState(false)
  const [error, setError] = useState('')
  const [copiedUrl, setCopiedUrl] = useState(false)

  const [formData, setFormData] = useState({
    companyName: '',
    signupBonusCash: 5.00,
    signupBonusPoints: 100,
    slug: '',
    loyaltySystemType: 'cashback',
    cashbackRate: 5.00,
    pointsRate: 1.00,
    historicalRewardDays: 14,
    welcomeIncentive: 5.00,
    brandingLogoUrl: '',
    brandingPrimaryColor: '#DC2626',
    brandingBackgroundColor: '#DC2626',
    brandingTextColor: '#FFFFFF',
    brandingButtonColor: '#E5E7EB',
    brandingButtonTextColor: '#1F2937',
    brandingHeadlineText: "You've earned:",
    brandingSubheadlineText: 'Register now to claim your rewards and save on future visits!',
    brandingQrHeadlineText: 'Scan to claim your rewards!',
    brandingQrSubheadlineText: 'Register now to claim your rewards and save on future visits!',
    brandingQrButtonText: 'Done',
    brandingRecognizedHeadlineText: 'Welcome back!',
    brandingRecognizedSubheadlineText: "You've earned:",
    brandingRecognizedButtonText: 'Skip',
    brandingRecognizedLinkText: "Don't show me again",
    // Wallet Pass Settings
    walletPassEnabled: true,
    walletPassDescription: '',
    walletPassIconUrl: '',
    walletPassStripUrl: '',
    walletPassLabelColor: '#FFFFFF',
    walletPassForegroundColor: '#FFFFFF'
  })

  useEffect(() => {
    document.title = 'Settings | Lemonade Loyalty'
  }, [])

  useEffect(() => {
    if (!user) {
      navigate('/signin')
      return
    }
    loadAccount()
  }, [user, navigate])

  const loadAccount = async () => {
    try {
      setLoading(true)
      const { data: { session } } = await supabase.auth.getSession()

      if (!session) {
        navigate('/signin')
        return
      }

      const response = await fetch('/api/accounts/me', {
        headers: {
          'Authorization': `Bearer ${session.access_token}`
        }
      })

      if (response.ok) {
        const data = await response.json()
        setAccount(data)
        setFormData({
          companyName: data.companyName || '',
          signupBonusCash: data.signupBonusCash || 5.00,
          signupBonusPoints: data.signupBonusPoints || 100,
          slug: data.slug || '',
          loyaltySystemType: data.loyaltySystemType || 'cashback',
          cashbackRate: data.cashbackRate || 5.00,
          pointsRate: data.pointsRate || 1.00,
          historicalRewardDays: data.historicalRewardDays || 14,
          welcomeIncentive: data.welcomeIncentive || 5.00,
          brandingLogoUrl: data.brandingLogoUrl || '',
          brandingPrimaryColor: data.brandingPrimaryColor || '#DC2626',
          brandingBackgroundColor: data.brandingBackgroundColor || '#DC2626',
          brandingTextColor: data.brandingTextColor || '#FFFFFF',
          brandingButtonColor: data.brandingButtonColor || '#E5E7EB',
          brandingButtonTextColor: data.brandingButtonTextColor || '#1F2937',
          brandingHeadlineText: data.brandingHeadlineText || "You've earned:",
          brandingSubheadlineText: data.brandingSubheadlineText || 'Register now to claim your rewards and save on future visits!',
          brandingQrHeadlineText: data.brandingQrHeadlineText || 'Scan to claim your rewards!',
          brandingQrSubheadlineText: data.brandingQrSubheadlineText || 'Register now to claim your rewards and save on future visits!',
          brandingQrButtonText: data.brandingQrButtonText || 'Done',
          brandingRecognizedHeadlineText: data.brandingRecognizedHeadlineText || 'Welcome back!',
          brandingRecognizedSubheadlineText: data.brandingRecognizedSubheadlineText || "You've earned:",
          brandingRecognizedButtonText: data.brandingRecognizedButtonText || 'Skip',
          brandingRecognizedLinkText: data.brandingRecognizedLinkText || "Don't show me again",
          // Wallet Pass Settings
          walletPassEnabled: data.walletPassEnabled ?? true,
          walletPassDescription: data.walletPassDescription || '',
          walletPassIconUrl: data.walletPassIconUrl || '',
          walletPassStripUrl: data.walletPassStripUrl || '',
          walletPassLabelColor: data.walletPassLabelColor || '#FFFFFF',
          walletPassForegroundColor: data.walletPassForegroundColor || '#FFFFFF'
        })
      }
    } catch (error) {
      console.error('Error loading account:', error)
      setError('Failed to load account settings')
    } finally {
      setLoading(false)
    }
  }

  const handleSave = async (e) => {
    e.preventDefault()
    setSaving(true)
    setError('')
    setSuccess(false)

    try {
      const { data: { session } } = await supabase.auth.getSession()

      const response = await fetch('/api/accounts/me', {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${session.access_token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          companyName: formData.companyName,
          signupBonusCash: parseFloat(formData.signupBonusCash),
          signupBonusPoints: parseInt(formData.signupBonusPoints),
          slug: formData.slug,
          loyaltySystemType: formData.loyaltySystemType,
          cashbackRate: parseFloat(formData.cashbackRate),
          pointsRate: parseFloat(formData.pointsRate),
          historicalRewardDays: parseInt(formData.historicalRewardDays),
          welcomeIncentive: parseFloat(formData.welcomeIncentive),
          brandingLogoUrl: formData.brandingLogoUrl || null,
          brandingPrimaryColor: formData.brandingPrimaryColor,
          brandingBackgroundColor: formData.brandingBackgroundColor,
          brandingTextColor: formData.brandingTextColor,
          brandingButtonColor: formData.brandingButtonColor,
          brandingButtonTextColor: formData.brandingButtonTextColor,
          brandingHeadlineText: formData.brandingHeadlineText,
          brandingSubheadlineText: formData.brandingSubheadlineText,
          brandingQrHeadlineText: formData.brandingQrHeadlineText,
          brandingQrSubheadlineText: formData.brandingQrSubheadlineText,
          brandingQrButtonText: formData.brandingQrButtonText,
          brandingRecognizedHeadlineText: formData.brandingRecognizedHeadlineText,
          brandingRecognizedSubheadlineText: formData.brandingRecognizedSubheadlineText,
          brandingRecognizedButtonText: formData.brandingRecognizedButtonText,
          brandingRecognizedLinkText: formData.brandingRecognizedLinkText,
          walletPassEnabled: formData.walletPassEnabled,
          walletPassDescription: formData.walletPassDescription || null,
          walletPassIconUrl: formData.walletPassIconUrl || null,
          walletPassStripUrl: formData.walletPassStripUrl || null,
          walletPassLabelColor: formData.walletPassLabelColor,
          walletPassForegroundColor: formData.walletPassForegroundColor
        })
      })

      if (!response.ok) {
        const data = await response.json()
        throw new Error(data.error || 'Failed to save settings')
      }

      const updatedAccount = await response.json()
      setAccount(updatedAccount)
      setSuccess(true)

      // Hide success message after 3 seconds
      setTimeout(() => setSuccess(false), 3000)
    } catch (error) {
      console.error('Error saving settings:', error)
      setError(error.message)
    } finally {
      setSaving(false)
    }
  }

  const generateSlug = () => {
    if (!formData.companyName) return

    const slug = formData.companyName
      .toLowerCase()
      .replace(/[^a-z0-9\s-]/g, '')
      .replace(/\s+/g, '-')
      .replace(/-+/g, '-')
      .trim()

    setFormData({ ...formData, slug })
  }

  const copySignupUrl = () => {
    const url = `${window.location.origin}/signup/${formData.slug}`
    navigator.clipboard.writeText(url)
    setCopiedUrl(true)
    setTimeout(() => setCopiedUrl(false), 2000)
  }

  if (loading) {
    return (
      <SidebarProvider>
        <AppSidebar account={account} />
        <SidebarInset>
          <div className="flex items-center justify-center h-screen">
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
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
                  <BreadcrumbPage>Settings</BreadcrumbPage>
                </BreadcrumbItem>
              </BreadcrumbList>
            </Breadcrumb>
          </div>
        </header>

        <div className="flex flex-1 flex-col gap-6 p-6">
          {/* Page Header */}
          <div>
            <h1 className="text-3xl font-semibold">Settings</h1>
            <p className="text-muted-foreground mt-2">
              Manage your loyalty program settings and branding
            </p>
          </div>

          {/* Success Alert */}
          {success && (
            <Alert className="border-green-200 bg-green-50">
              <CheckCircle2 className="h-4 w-4 text-green-600" />
              <AlertDescription className="text-green-800">
                Settings saved successfully!
              </AlertDescription>
            </Alert>
          )}

          {/* Error Alert */}
          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <form onSubmit={handleSave} className="space-y-6">
            {/* Company Details Section */}
            <div className="border rounded-lg">
              <div className="px-6 py-4 border-b bg-muted/30">
                <h2 className="text-lg font-semibold">Company details</h2>
                <p className="text-sm text-muted-foreground mt-1">
                  Your business information shown to customers
                </p>
              </div>
              <div className="px-6 py-6 space-y-6">
                <div className="grid gap-6 md:grid-cols-2">
                  <div className="space-y-2">
                    <Label htmlFor="companyName">Company name</Label>
                    <Input
                      id="companyName"
                      type="text"
                      placeholder="Acme Coffee Shop"
                      required
                      value={formData.companyName}
                      onChange={(e) => setFormData({ ...formData, companyName: e.target.value })}
                      disabled={saving}
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="slug">URL slug</Label>
                    <div className="flex gap-2">
                      <Input
                        id="slug"
                        type="text"
                        placeholder="acme-coffee-shop"
                        required
                        value={formData.slug}
                        onChange={(e) => setFormData({ ...formData, slug: e.target.value })}
                        disabled={saving}
                        pattern="[a-z0-9-]+"
                        title="Only lowercase letters, numbers, and hyphens allowed"
                        className="flex-1"
                      />
                      <Button
                        type="button"
                        variant="outline"
                        onClick={generateSlug}
                        disabled={saving || !formData.companyName}
                      >
                        Generate
                      </Button>
                    </div>
                  </div>
                </div>

                {formData.slug && (
                  <div className="flex items-center gap-2 p-4 bg-muted rounded-lg">
                    <LinkIcon className="h-4 w-4 text-muted-foreground flex-shrink-0" />
                    <code className="text-sm flex-1 truncate">
                      {window.location.origin}/signup/{formData.slug}
                    </code>
                    <Button
                      type="button"
                      size="sm"
                      variant="ghost"
                      onClick={copySignupUrl}
                      className="flex-shrink-0"
                    >
                      {copiedUrl ? (
                        <>
                          <Check className="h-4 w-4 mr-2" />
                          Copied
                        </>
                      ) : (
                        <>
                          <Copy className="h-4 w-4 mr-2" />
                          Copy
                        </>
                      )}
                    </Button>
                  </div>
                )}
              </div>
            </div>

            {/* Loyalty System Configuration */}
            <div className="border rounded-lg">
              <div className="px-6 py-4 border-b bg-muted/30">
                <h2 className="text-lg font-semibold">Loyalty System</h2>
                <p className="text-sm text-muted-foreground mt-1">
                  Configure how customers earn rewards
                </p>
              </div>
              <div className="px-6 py-6 space-y-6">
                {/* Loyalty Type Radio Buttons */}
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="cashbackRate">Cashback rate (%)</Label>
                    <Input
                      id="cashbackRate"
                      type="number"
                      min="0"
                      max="100"
                      step="0.01"
                      value={formData.cashbackRate}
                      onChange={(e) => setFormData({ ...formData, cashbackRate: parseFloat(e.target.value) || 0 })}
                      disabled={saving}
                    />
                    <p className="text-xs text-muted-foreground">
                      Customers earn this percentage as cashback (e.g., 5.00 = 5%)
                    </p>
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="welcomeIncentive">Welcome incentive ($)</Label>
                    <Input
                      id="welcomeIncentive"
                      type="number"
                      min="0"
                      step="0.01"
                      value={formData.welcomeIncentive}
                      onChange={(e) => setFormData({ ...formData, welcomeIncentive: parseFloat(e.target.value) || 0 })}
                      disabled={saving}
                    />
                    <p className="text-xs text-muted-foreground">
                      One-time bonus when customer links their card
                    </p>
                  </div>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="historicalRewardDays">Historical reward period (days)</Label>
                  <Input
                    id="historicalRewardDays"
                    type="number"
                    min="0"
                    max="365"
                    value={formData.historicalRewardDays}
                    onChange={(e) => setFormData({ ...formData, historicalRewardDays: parseInt(e.target.value) || 0 })}
                    disabled={saving}
                    className="max-w-xs"
                  />
                  <p className="text-xs text-muted-foreground">
                    Reward purchases made this many days before linking (0 to disable)
                  </p>
                </div>
              </div>
            </div>

            {/* Rewards Section */}
            <div className="border rounded-lg">
              <div className="px-6 py-4 border-b bg-muted/30">
                <h2 className="text-lg font-semibold">Rewards</h2>
                <p className="text-sm text-muted-foreground mt-1">
                  Configure points and loyalty program rules
                </p>
              </div>
              <div className="px-6 py-6 space-y-6">
                <div className="space-y-2">
                  <Label htmlFor="signupBonusCash">Sign-up bonus</Label>
                  <div className="flex items-center gap-2">
                    <span className="text-sm text-muted-foreground">$</span>
                    <Input
                      id="signupBonusCash"
                      type="number"
                      min="0"
                      step="0.01"
                      required
                      value={formData.signupBonusCash}
                      onChange={(e) => setFormData({ ...formData, signupBonusCash: parseFloat(e.target.value) || 0 })}
                      disabled={saving}
                      className="max-w-[200px]"
                    />
                  </div>
                  <p className="text-sm text-muted-foreground">
                    Cash bonus awarded when a customer first registers
                  </p>
                </div>

                <Separator />

                <div className="space-y-3">
                  <p className="text-sm font-medium">Earning rules</p>
                  <div className="space-y-2 text-sm text-muted-foreground">
                    <div className="flex items-start gap-2">
                      <span className="text-green-600 mt-0.5">✓</span>
                      <span>Customers earn {formData.cashbackRate}% cashback on all purchases</span>
                    </div>
                    <div className="flex items-start gap-2">
                      <span className="text-green-600 mt-0.5">✓</span>
                      <span>Signup bonus of ${formData.signupBonusCash} cash when they register</span>
                    </div>
                    <div className="flex items-start gap-2">
                      <span className="text-green-600 mt-0.5">✓</span>
                      <span>Unclaimed cashback from previous purchases are credited upon signup</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* Branding Section */}
            <div className="border rounded-lg">
              <div className="px-6 py-4 border-b bg-muted/30">
                <h2 className="text-lg font-semibold">Branding</h2>
                <p className="text-sm text-muted-foreground mt-1">
                  Customize how your brand appears on terminal screens
                </p>
              </div>
              <div className="px-6 py-6 space-y-6">
                {/* Logo */}
                <div className="space-y-2">
                  <Label htmlFor="brandingLogoUrl">Logo URL</Label>
                  <Input
                    id="brandingLogoUrl"
                    type="url"
                    placeholder="https://example.com/logo.png"
                    value={formData.brandingLogoUrl}
                    onChange={(e) => setFormData({ ...formData, brandingLogoUrl: e.target.value })}
                    disabled={saving}
                  />
                  <p className="text-sm text-muted-foreground">
                    PNG or JPG format, square recommended
                  </p>
                </div>

                <Separator />

                {/* Colors */}
                <div className="space-y-4">
                  <p className="text-sm font-medium">Colors</p>
                  <div className="grid gap-4 md:grid-cols-2">
                    <div className="space-y-2">
                      <Label htmlFor="brandingBackgroundColor">Background</Label>
                      <div className="flex gap-2">
                        <Input
                          id="brandingBackgroundColor"
                          type="color"
                          value={formData.brandingBackgroundColor}
                          onChange={(e) => setFormData({ ...formData, brandingBackgroundColor: e.target.value })}
                          disabled={saving}
                          className="w-14 h-10 p-1"
                        />
                        <Input
                          type="text"
                          value={formData.brandingBackgroundColor}
                          onChange={(e) => setFormData({ ...formData, brandingBackgroundColor: e.target.value })}
                          disabled={saving}
                          placeholder="#DC2626"
                          className="flex-1"
                        />
                      </div>
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="brandingTextColor">Text</Label>
                      <div className="flex gap-2">
                        <Input
                          id="brandingTextColor"
                          type="color"
                          value={formData.brandingTextColor}
                          onChange={(e) => setFormData({ ...formData, brandingTextColor: e.target.value })}
                          disabled={saving}
                          className="w-14 h-10 p-1"
                        />
                        <Input
                          type="text"
                          value={formData.brandingTextColor}
                          onChange={(e) => setFormData({ ...formData, brandingTextColor: e.target.value })}
                          disabled={saving}
                          placeholder="#FFFFFF"
                          className="flex-1"
                        />
                      </div>
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="brandingButtonColor">Button background</Label>
                      <div className="flex gap-2">
                        <Input
                          id="brandingButtonColor"
                          type="color"
                          value={formData.brandingButtonColor}
                          onChange={(e) => setFormData({ ...formData, brandingButtonColor: e.target.value })}
                          disabled={saving}
                          className="w-14 h-10 p-1"
                        />
                        <Input
                          type="text"
                          value={formData.brandingButtonColor}
                          onChange={(e) => setFormData({ ...formData, brandingButtonColor: e.target.value })}
                          disabled={saving}
                          placeholder="#E5E7EB"
                          className="flex-1"
                        />
                      </div>
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="brandingButtonTextColor">Button text</Label>
                      <div className="flex gap-2">
                        <Input
                          id="brandingButtonTextColor"
                          type="color"
                          value={formData.brandingButtonTextColor}
                          onChange={(e) => setFormData({ ...formData, brandingButtonTextColor: e.target.value })}
                          disabled={saving}
                          className="w-14 h-10 p-1"
                        />
                        <Input
                          type="text"
                          value={formData.brandingButtonTextColor}
                          onChange={(e) => setFormData({ ...formData, brandingButtonTextColor: e.target.value })}
                          disabled={saving}
                          placeholder="#1F2937"
                          className="flex-1"
                        />
                      </div>
                    </div>
                  </div>
                </div>

                <Separator />

                {/* Preview */}
                <div className="space-y-3">
                  <p className="text-sm font-medium">Preview</p>
                  <div className="border-2 border-dashed rounded-lg p-4">
                    <div
                      className="rounded-lg p-8 text-center max-w-md mx-auto"
                      style={{ backgroundColor: formData.brandingBackgroundColor }}
                    >
                      {formData.brandingLogoUrl && (
                        <div className="flex justify-center mb-4">
                          <div className="w-20 h-20 bg-white rounded-lg flex items-center justify-center p-2">
                            <img
                              src={formData.brandingLogoUrl}
                              alt="Logo"
                              className="max-w-full max-h-full object-contain"
                              onError={(e) => e.target.style.display = 'none'}
                            />
                          </div>
                        </div>
                      )}
                      <p
                        className="text-sm font-medium mb-2"
                        style={{ color: formData.brandingTextColor }}
                      >
                        {formData.brandingHeadlineText}
                      </p>
                      <p
                        className="text-3xl font-bold mb-2"
                        style={{ color: formData.brandingTextColor }}
                      >
                        120 points
                      </p>
                      <p
                        className="text-sm mb-4 opacity-90"
                        style={{ color: formData.brandingTextColor }}
                      >
                        {formData.brandingSubheadlineText}
                      </p>
                      <button
                        className="px-6 py-2.5 rounded-lg font-medium text-sm"
                        style={{
                          backgroundColor: formData.brandingButtonColor,
                          color: formData.brandingButtonTextColor
                        }}
                      >
                        Claim Rewards
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* Terminal Screen Text Section */}
            <div className="border rounded-lg">
              <div className="px-6 py-4 border-b bg-muted/30">
                <h2 className="text-lg font-semibold">Terminal screen text</h2>
                <p className="text-sm text-muted-foreground mt-1">
                  Customize messages shown on payment terminal screens
                </p>
              </div>
              <div className="px-6 py-6 space-y-6">
                {/* Earn Points Screen */}
                <div className="space-y-4">
                  <div className="flex items-center justify-between">
                    <h3 className="text-sm font-semibold">Earn points screen</h3>
                    <span className="text-xs text-muted-foreground">New customers</span>
                  </div>
                  <div className="space-y-4">
                    <div className="space-y-2">
                      <Label htmlFor="brandingHeadlineText" className="text-sm">Headline</Label>
                      <Input
                        id="brandingHeadlineText"
                        value={formData.brandingHeadlineText}
                        onChange={(e) => setFormData({ ...formData, brandingHeadlineText: e.target.value })}
                        disabled={saving}
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="brandingSubheadlineText" className="text-sm">Subheadline</Label>
                      <Textarea
                        id="brandingSubheadlineText"
                        value={formData.brandingSubheadlineText}
                        onChange={(e) => setFormData({ ...formData, brandingSubheadlineText: e.target.value })}
                        disabled={saving}
                        rows={2}
                      />
                    </div>
                  </div>
                </div>

                <Separator />

                {/* QR Scan Screen */}
                <div className="space-y-4">
                  <div className="flex items-center justify-between">
                    <h3 className="text-sm font-semibold">QR scan screen</h3>
                    <span className="text-xs text-muted-foreground">Sign-up flow</span>
                  </div>
                  <div className="space-y-4">
                    <div className="space-y-2">
                      <Label htmlFor="brandingQrHeadlineText" className="text-sm">Headline</Label>
                      <Input
                        id="brandingQrHeadlineText"
                        value={formData.brandingQrHeadlineText}
                        onChange={(e) => setFormData({ ...formData, brandingQrHeadlineText: e.target.value })}
                        disabled={saving}
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="brandingQrSubheadlineText" className="text-sm">Subheadline</Label>
                      <Textarea
                        id="brandingQrSubheadlineText"
                        value={formData.brandingQrSubheadlineText}
                        onChange={(e) => setFormData({ ...formData, brandingQrSubheadlineText: e.target.value })}
                        disabled={saving}
                        rows={2}
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="brandingQrButtonText" className="text-sm">Button text</Label>
                      <Input
                        id="brandingQrButtonText"
                        value={formData.brandingQrButtonText}
                        onChange={(e) => setFormData({ ...formData, brandingQrButtonText: e.target.value })}
                        disabled={saving}
                        className="max-w-[200px]"
                      />
                    </div>
                  </div>
                </div>

                <Separator />

                {/* Welcome Back Screen */}
                <div className="space-y-4">
                  <div className="flex items-center justify-between">
                    <h3 className="text-sm font-semibold">Welcome back screen</h3>
                    <span className="text-xs text-muted-foreground">Returning customers</span>
                  </div>
                  <div className="space-y-4">
                    <div className="space-y-2">
                      <Label htmlFor="brandingRecognizedHeadlineText" className="text-sm">Headline</Label>
                      <Input
                        id="brandingRecognizedHeadlineText"
                        value={formData.brandingRecognizedHeadlineText}
                        onChange={(e) => setFormData({ ...formData, brandingRecognizedHeadlineText: e.target.value })}
                        disabled={saving}
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="brandingRecognizedSubheadlineText" className="text-sm">Subheadline</Label>
                      <Input
                        id="brandingRecognizedSubheadlineText"
                        value={formData.brandingRecognizedSubheadlineText}
                        onChange={(e) => setFormData({ ...formData, brandingRecognizedSubheadlineText: e.target.value })}
                        disabled={saving}
                      />
                    </div>
                    <div className="grid gap-4 md:grid-cols-2">
                      <div className="space-y-2">
                        <Label htmlFor="brandingRecognizedButtonText" className="text-sm">Button text</Label>
                        <Input
                          id="brandingRecognizedButtonText"
                          value={formData.brandingRecognizedButtonText}
                          onChange={(e) => setFormData({ ...formData, brandingRecognizedButtonText: e.target.value })}
                          disabled={saving}
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="brandingRecognizedLinkText" className="text-sm">Link text</Label>
                        <Input
                          id="brandingRecognizedLinkText"
                          value={formData.brandingRecognizedLinkText}
                          onChange={(e) => setFormData({ ...formData, brandingRecognizedLinkText: e.target.value })}
                          disabled={saving}
                        />
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* Wallet Pass Settings */}
            <div className="border rounded-lg">
              <div className="px-6 py-4 border-b bg-muted/30">
                <div className="flex items-center gap-2">
                  <Smartphone className="h-5 w-5" />
                  <h2 className="text-lg font-semibold">Wallet Pass</h2>
                </div>
                <p className="text-sm text-muted-foreground mt-1">
                  Configure Apple Wallet and Google Wallet passes for your customers
                </p>
              </div>
              <div className="px-6 py-6 space-y-6">
                {/* Enable/Disable */}
                <div className="flex items-center justify-between">
                  <div className="space-y-0.5">
                    <Label htmlFor="walletPassEnabled">Enable wallet passes</Label>
                    <p className="text-sm text-muted-foreground">
                      Allow customers to add their loyalty card to Apple Wallet or Google Wallet
                    </p>
                  </div>
                  <Switch
                    id="walletPassEnabled"
                    checked={formData.walletPassEnabled}
                    onCheckedChange={(checked) => setFormData({ ...formData, walletPassEnabled: checked })}
                    disabled={saving}
                  />
                </div>

                {formData.walletPassEnabled && (
                  <>
                    <Separator />

                    {/* Description */}
                    <div className="space-y-2">
                      <Label htmlFor="walletPassDescription">Pass description</Label>
                      <Input
                        id="walletPassDescription"
                        placeholder="Your loyalty card for Acme Coffee"
                        value={formData.walletPassDescription}
                        onChange={(e) => setFormData({ ...formData, walletPassDescription: e.target.value })}
                        disabled={saving}
                      />
                      <p className="text-sm text-muted-foreground">
                        A short description shown on the pass (e.g., "Your loyalty card for [Company Name]")
                      </p>
                    </div>

                    <Separator />

                    {/* Images */}
                    <div className="space-y-4">
                      <p className="text-sm font-medium">Pass images</p>
                      <div className="grid gap-4 md:grid-cols-2">
                        <div className="space-y-2">
                          <Label htmlFor="walletPassIconUrl">Icon URL</Label>
                          <Input
                            id="walletPassIconUrl"
                            type="url"
                            placeholder="https://example.com/icon.png"
                            value={formData.walletPassIconUrl}
                            onChange={(e) => setFormData({ ...formData, walletPassIconUrl: e.target.value })}
                            disabled={saving}
                          />
                          <p className="text-xs text-muted-foreground">
                            Square icon (87x87, 174x174, or 261x261 px)
                          </p>
                        </div>
                        <div className="space-y-2">
                          <Label htmlFor="walletPassStripUrl">Strip image URL (Apple)</Label>
                          <Input
                            id="walletPassStripUrl"
                            type="url"
                            placeholder="https://example.com/strip.png"
                            value={formData.walletPassStripUrl}
                            onChange={(e) => setFormData({ ...formData, walletPassStripUrl: e.target.value })}
                            disabled={saving}
                          />
                          <p className="text-xs text-muted-foreground">
                            Banner image for Apple Wallet (375x123, 750x246, or 1125x369 px)
                          </p>
                        </div>
                      </div>
                    </div>

                    <Separator />

                    {/* Colors */}
                    <div className="space-y-4">
                      <p className="text-sm font-medium">Pass text colors</p>
                      <div className="grid gap-4 md:grid-cols-2">
                        <div className="space-y-2">
                          <Label htmlFor="walletPassLabelColor">Label color</Label>
                          <div className="flex gap-2">
                            <Input
                              id="walletPassLabelColor"
                              type="color"
                              value={formData.walletPassLabelColor}
                              onChange={(e) => setFormData({ ...formData, walletPassLabelColor: e.target.value })}
                              disabled={saving}
                              className="w-14 h-10 p-1"
                            />
                            <Input
                              type="text"
                              value={formData.walletPassLabelColor}
                              onChange={(e) => setFormData({ ...formData, walletPassLabelColor: e.target.value })}
                              disabled={saving}
                              placeholder="#FFFFFF"
                              className="flex-1"
                            />
                          </div>
                          <p className="text-xs text-muted-foreground">
                            Color for field labels on the pass
                          </p>
                        </div>
                        <div className="space-y-2">
                          <Label htmlFor="walletPassForegroundColor">Value color</Label>
                          <div className="flex gap-2">
                            <Input
                              id="walletPassForegroundColor"
                              type="color"
                              value={formData.walletPassForegroundColor}
                              onChange={(e) => setFormData({ ...formData, walletPassForegroundColor: e.target.value })}
                              disabled={saving}
                              className="w-14 h-10 p-1"
                            />
                            <Input
                              type="text"
                              value={formData.walletPassForegroundColor}
                              onChange={(e) => setFormData({ ...formData, walletPassForegroundColor: e.target.value })}
                              disabled={saving}
                              placeholder="#FFFFFF"
                              className="flex-1"
                            />
                          </div>
                          <p className="text-xs text-muted-foreground">
                            Color for field values on the pass
                          </p>
                        </div>
                      </div>
                    </div>

                    <Separator />

                    {/* Pass Preview */}
                    <div className="space-y-4">
                      <p className="text-sm font-medium">Pass preview</p>
                      <div
                        className="rounded-xl p-4 max-w-sm mx-auto shadow-lg"
                        style={{ backgroundColor: formData.brandingBackgroundColor }}
                      >
                        <div className="flex items-center gap-3 mb-4">
                          {formData.walletPassIconUrl ? (
                            <img
                              src={formData.walletPassIconUrl}
                              alt="Pass icon"
                              className="w-12 h-12 rounded-lg object-cover"
                            />
                          ) : (
                            <div
                              className="w-12 h-12 rounded-lg flex items-center justify-center text-lg font-bold"
                              style={{ backgroundColor: 'rgba(255,255,255,0.2)', color: formData.walletPassForegroundColor }}
                            >
                              {formData.companyName?.charAt(0) || 'L'}
                            </div>
                          )}
                          <div>
                            <p
                              className="font-semibold"
                              style={{ color: formData.walletPassForegroundColor }}
                            >
                              {formData.companyName || 'Company Name'}
                            </p>
                            <p
                              className="text-sm opacity-80"
                              style={{ color: formData.walletPassLabelColor }}
                            >
                              Loyalty Card
                            </p>
                          </div>
                        </div>
                        {formData.walletPassStripUrl && (
                          <img
                            src={formData.walletPassStripUrl}
                            alt="Pass strip"
                            className="w-full rounded-lg mb-4 object-cover"
                            style={{ maxHeight: '100px' }}
                          />
                        )}
                        <div className="space-y-2">
                          <div>
                            <p
                              className="text-xs uppercase tracking-wide"
                              style={{ color: formData.walletPassLabelColor }}
                            >
                              Balance
                            </p>
                            <p
                              className="text-2xl font-bold"
                              style={{ color: formData.walletPassForegroundColor }}
                            >
                              $12.50
                            </p>
                          </div>
                        </div>
                      </div>
                      <p className="text-xs text-muted-foreground text-center">
                        This is an approximate preview. Actual appearance may vary by device.
                      </p>
                    </div>
                  </>
                )}
              </div>
            </div>

            {/* Save Button */}
            <div className="flex items-center justify-between pt-4 border-t">
              <p className="text-sm text-muted-foreground">
                Changes will be applied immediately to all terminals
              </p>
              <Button type="submit" disabled={saving}>
                {saving ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Saving...
                  </>
                ) : (
                  'Save changes'
                )}
              </Button>
            </div>
          </form>
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}

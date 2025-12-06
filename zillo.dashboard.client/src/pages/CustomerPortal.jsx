import { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Button } from '@/components/ui/button';
import { Loader2, CreditCard, TrendingUp, TrendingDown, Gift, Calendar, Wallet, Download, X } from 'lucide-react';

export default function CustomerPortal() {
  const { token } = useParams();
  const [portalData, setPortalData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [activeTab, setActiveTab] = useState('history');
  const [showRedeemModal, setShowRedeemModal] = useState(false);
  const [walletLoading, setWalletLoading] = useState(false);

  // Detect if user is on iOS or Android
  const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent);
  const isAndroid = /Android/.test(navigator.userAgent);

  const handleAddToAppleWallet = async () => {
    if (!token) return;
    setWalletLoading(true);
    try {
      // Download the .pkpass file
      const response = await fetch(`/api/wallet/apple/pass/${token}`);
      if (!response.ok) {
        throw new Error('Failed to generate pass');
      }
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'loyalty.pkpass';
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (err) {
      console.error('Error adding to Apple Wallet:', err);
      alert('Failed to add to Apple Wallet. Please try again.');
    } finally {
      setWalletLoading(false);
    }
  };

  const handleAddToGoogleWallet = async () => {
    if (!token) return;
    setWalletLoading(true);
    try {
      const response = await fetch(`/api/wallet/google/pass/${token}`);
      if (!response.ok) {
        throw new Error('Failed to generate pass');
      }
      const data = await response.json();
      // Open the Google Wallet save URL in a new tab
      window.open(data.saveUrl, '_blank');
    } catch (err) {
      console.error('Error adding to Google Wallet:', err);
      alert('Failed to add to Google Wallet. Please try again.');
    } finally {
      setWalletLoading(false);
    }
  };

  useEffect(() => {
    if (portalData?.name) {
      const companyName = portalData.companyName || 'Lemonade';
      document.title = `${portalData.name} | ${companyName} Loyalty`;
    } else {
      document.title = 'Customer Portal | Loyalty Rewards';
    }
  }, [portalData]);

  useEffect(() => {
    if (!token) {
      setError('Invalid portal link. Token is required.');
      setLoading(false);
      return;
    }

    fetchPortalData();
  }, [token]);

  const fetchPortalData = async () => {
    try {
      const response = await fetch(`/api/customer-portal/${token}`);

      if (!response.ok) {
        const data = await response.json();
        throw new Error(data.error || 'Failed to load portal data');
      }

      const data = await response.json();
      setPortalData(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const formatDate = (dateString) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  const getTransactionIcon = (type) => {
    switch (type) {
      case 'earn':
      case 'cashback_earn':
        return <TrendingUp className="h-4 w-4 text-green-600" />;
      case 'redeem':
      case 'cashback_redeem':
        return <TrendingDown className="h-4 w-4 text-red-600" />;
      case 'bonus':
      case 'welcome_bonus':
        return <Gift className="h-4 w-4 text-indigo-600" />;
      default:
        return <Calendar className="h-4 w-4 text-gray-600" />;
    }
  };

  const getTransactionColor = (type) => {
    switch (type) {
      case 'earn':
      case 'cashback_earn':
        return 'text-green-600';
      case 'redeem':
      case 'cashback_redeem':
        return 'text-red-600';
      case 'bonus':
      case 'welcome_bonus':
        return 'text-indigo-600';
      default:
        return 'text-gray-600';
    }
  };

  const getTransactionLabel = (type) => {
    switch (type) {
      case 'cashback_earn':
        return 'Cashback Earned';
      case 'cashback_redeem':
        return 'Cashback Redeemed';
      case 'welcome_bonus':
        return 'Welcome Bonus';
      case 'earn':
        return 'Credit Earned';
      case 'redeem':
        return 'Credit Redeemed';
      case 'adjustment':
        return 'Adjustment';
      default:
        return type;
    }
  };

  const getCardBrandIcon = (brand) => {
    const brandLower = (brand || '').toLowerCase();

    // Card brand SVG icons as data URIs
    const icons = {
      visa: 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 32"%3E%3Crect width="48" height="32" rx="4" fill="%231434CB"/%3E%3Cpath d="M18.5 11.5h-2.8l-1.8 9h2.8l1.8-9zm7.8 5.8l1.5-4.1.8 4.1h-2.3zm3.2 3.2h2.6l-2.3-9h-2.3c-.5 0-.9.3-1.1.7l-3.8 8.3h2.9l.6-1.6h3.6l.3 1.6zm-7.5-2.9c0-2.4-3.3-2.5-3.3-3.5 0-.3.3-.7 1-.7.6 0 1.5.1 2.2.5l.4-1.9c-.7-.2-1.5-.4-2.5-.4-2.6 0-4.5 1.4-4.5 3.3 0 1.4 1.3 2.2 2.3 2.7 1 .5 1.4.8 1.4 1.2 0 .7-.8 1-1.5 1-.9 0-1.9-.2-2.7-.6l-.5 2c.8.3 2 .6 3.2.6 2.8 0 4.5-1.4 4.5-3.5z" fill="white"/%3E%3C/svg%3E',
      mastercard: 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 32"%3E%3Crect width="48" height="32" rx="4" fill="%23EB001B"/%3E%3Ccircle cx="18" cy="16" r="9" fill="%23EB001B"/%3E%3Ccircle cx="30" cy="16" r="9" fill="%23F79E1B"/%3E%3Cpath d="M24 9.5c-1.5 1.7-2.5 3.9-2.5 6.5s1 4.8 2.5 6.5c1.5-1.7 2.5-3.9 2.5-6.5s-1-4.8-2.5-6.5z" fill="%23FF5F00"/%3E%3C/svg%3E',
      amex: 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 32"%3E%3Crect width="48" height="32" rx="4" fill="%23006FCF"/%3E%3Ctext x="24" y="20" font-family="Arial,sans-serif" font-size="8" font-weight="bold" fill="white" text-anchor="middle"%3EAMEX%3C/text%3E%3C/svg%3E',
      discover: 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 32"%3E%3Crect width="48" height="32" rx="4" fill="%23FF6000"/%3E%3Ctext x="24" y="20" font-family="Arial,sans-serif" font-size="7" font-weight="bold" fill="white" text-anchor="middle"%3EDISCOVER%3C/text%3E%3C/svg%3E'
    };

    return icons[brandLower] || null;
  };

  // Extract branding with defaults
  const branding = portalData?.branding || {
    logoUrl: null,
    primaryColor: '#DC2626',
    backgroundColor: '#DC2626',
    textColor: '#FFFFFF',
    buttonColor: '#E5E7EB',
    buttonTextColor: '#1F2937'
  };

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center" style={{ backgroundColor: `${branding.backgroundColor}15` }}>
        <Loader2 className="h-8 w-8 animate-spin" style={{ color: branding.primaryColor }} />
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen flex items-center justify-center p-4" style={{ backgroundColor: `${branding.backgroundColor}15` }}>
        <Card className="w-full max-w-md">
          <CardHeader>
            <CardTitle className="text-red-600">Error</CardTitle>
          </CardHeader>
          <CardContent>
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="min-h-screen" style={{ backgroundColor: branding.backgroundColor }}>
      {/* Header with Logo/Company Name */}
      <div className="px-6 pt-6 pb-4">
        <div className="flex items-center justify-center mb-8">
          {branding.logoUrl ? (
            <img
              src={branding.logoUrl}
              alt={portalData?.companyName}
              className="h-10 w-auto object-contain"
            />
          ) : (
            <h2 className="text-xl font-semibold" style={{ color: branding.textColor }}>
              {portalData?.companyName}
            </h2>
          )}
        </div>

        {/* Greeting */}
        <h1 className="text-2xl font-normal mb-6 text-center" style={{ color: branding.textColor }}>
          Hi {portalData?.name?.split(' ')[0] || portalData?.name}!
        </h1>

        {/* Balance Display */}
        <div className="text-center mb-6">
          {portalData?.loyaltySystemType === 'cashback' ? (
            <p className="text-7xl font-bold mb-2" style={{ color: branding.textColor }}>
              ${((portalData?.cashbackBalance || 0) / 100).toFixed(2)}
            </p>
          ) : (
            <p className="text-7xl font-bold mb-2" style={{ color: branding.textColor }}>
              ${portalData?.pointsBalance || 0}
            </p>
          )}
          <p className="text-base" style={{ color: branding.textColor, opacity: 0.8 }}>
            Available Cash at {portalData?.companyName}
          </p>
          <p className="text-sm mt-1" style={{ color: branding.textColor, opacity: 0.7 }}>
            {portalData?.loyaltySystemType === 'cashback'
              ? `Earn ${portalData?.cashbackRate || 5}% cashback on every purchase`
              : `Earn $${portalData?.pointsRate || 1} for every $10 spent`
            }
          </p>
        </div>

        {/* Action Buttons */}
        <div className="flex gap-3 mb-6 items-center">
          <Button
            className="flex-1 h-12 rounded-lg font-medium"
            onClick={() => setShowRedeemModal(true)}
            style={{
              backgroundColor: branding.buttonColor,
              color: branding.buttonTextColor
            }}
          >
            <Wallet className="mr-2 h-5 w-5" />
            Redeem
          </Button>
        </div>

        {/* Wallet Buttons */}
        <div className="flex flex-col gap-2 mb-6">
          {/* Show Apple Wallet first on iOS, Google Wallet first on Android, both on desktop */}
          {(isIOS || !isAndroid) && (
            <button
              onClick={handleAddToAppleWallet}
              disabled={walletLoading}
              className="w-full flex justify-center disabled:opacity-50"
            >
              <img
                src="/images/apple-wallet-badge.svg"
                alt="Add to Apple Wallet"
                className="h-12 w-auto"
              />
            </button>
          )}
          {(isAndroid || !isIOS) && (
            <button
              onClick={handleAddToGoogleWallet}
              disabled={walletLoading}
              className="w-full flex justify-center disabled:opacity-50"
            >
              <img
                src="/images/google-wallet-badge.svg"
                alt="Add to Google Wallet"
                className="h-12 w-auto"
              />
            </button>
          )}
        </div>
      </div>

      {/* White Content Area with Tabs */}
      <div className="bg-white rounded-t-3xl min-h-screen pt-6">
        {/* Tabs */}
        <div className="flex gap-3 px-6 mb-6">
          <button
            onClick={() => setActiveTab('history')}
            className={`px-6 py-2 rounded-full text-sm font-medium transition-colors ${
              activeTab === 'history'
                ? 'bg-gray-200 text-gray-900'
                : 'bg-transparent text-gray-500'
            }`}
          >
            History
          </button>
          <button
            onClick={() => setActiveTab('cards')}
            className={`px-6 py-2 rounded-full text-sm font-medium transition-colors ${
              activeTab === 'cards'
                ? 'bg-gray-200 text-gray-900'
                : 'bg-transparent text-gray-500'
            }`}
          >
            Cards
          </button>
          <button
            onClick={() => setActiveTab('about')}
            className={`px-6 py-2 rounded-full text-sm font-medium transition-colors ${
              activeTab === 'about'
                ? 'bg-gray-200 text-gray-900'
                : 'bg-transparent text-gray-500'
            }`}
          >
            About you
          </button>
        </div>

        {/* Content Based on Active Tab */}
        {activeTab === 'history' ? (
          <div className="px-6">
            <h2 className="text-2xl font-semibold mb-6 text-gray-900">Activity</h2>

            {portalData?.recentTransactions && portalData.recentTransactions.length > 0 ? (
              <div className="space-y-4">
                {portalData.recentTransactions.map((transaction) => (
                  <div key={transaction.id} className="flex items-center justify-between py-3">
                    <div className="flex items-center gap-4">
                      <div
                        className="flex h-12 w-12 items-center justify-center rounded-full"
                        style={{ backgroundColor: `${branding.primaryColor}15` }}
                      >
                        {getTransactionIcon(transaction.type)}
                      </div>
                      <div>
                        <p className="font-medium text-gray-900">{getTransactionLabel(transaction.type)}</p>
                        <p className="text-sm text-gray-500">
                          {new Date(transaction.createdAt).toLocaleDateString('en-US', {
                            month: 'short',
                            day: 'numeric',
                            year: 'numeric'
                          })}
                        </p>
                      </div>
                    </div>
                    <div className="text-right">
                      {portalData?.loyaltySystemType === 'cashback' ? (
                        <p className={`text-lg font-bold ${getTransactionColor(transaction.type)}`}>
                          {(transaction.type === 'cashback_earn' || transaction.type === 'welcome_bonus' || transaction.type === 'bonus') ? '+' : ''}
                          ${((transaction.cashbackAmount || 0) / 100).toFixed(2)}
                        </p>
                      ) : (
                        <p className={`text-lg font-bold ${getTransactionColor(transaction.type)}`}>
                          {transaction.points > 0 ? '+' : ''}${transaction.points}
                        </p>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-gray-500 text-center py-12">No activity yet</p>
            )}
          </div>
        ) : activeTab === 'cards' ? (
          <div className="px-6">
            <h2 className="text-2xl font-semibold mb-6 text-gray-900">Registered Cards</h2>

            {portalData?.registeredCards && portalData.registeredCards.length > 0 ? (
              <div className="space-y-3">
                {portalData.registeredCards.map((card) => {
                  const cardIcon = getCardBrandIcon(card.cardBrand);
                  return (
                    <div
                      key={card.id}
                      className="flex items-center gap-3 p-4 border border-gray-200 rounded-xl"
                    >
                      {cardIcon ? (
                        <img
                          src={cardIcon}
                          alt={card.cardBrand}
                          className="h-8 w-12 object-contain"
                        />
                      ) : (
                        <div className="flex h-8 w-12 items-center justify-center rounded bg-gray-100">
                          <CreditCard className="h-5 w-5 text-gray-600" />
                        </div>
                      )}
                      <div className="flex-1">
                        <p className="font-medium text-gray-900 capitalize">
                          {card.cardBrand || 'Card'}
                        </p>
                        <p className="text-sm text-gray-500">•••• {card.cardLast4 || '****'}</p>
                      </div>
                      {card.isPrimary && (
                        <Badge variant="secondary">Primary</Badge>
                      )}
                    </div>
                  );
                })}
              </div>
            ) : (
              <p className="text-gray-500 text-center py-12">No cards registered yet</p>
            )}
          </div>
        ) : (
          <div className="px-6">
            <h2 className="text-2xl font-semibold mb-6 text-gray-900">About you</h2>

            <div className="space-y-4">
              <div>
                <p className="text-sm font-medium text-gray-500 mb-1">Name</p>
                <p className="text-base text-gray-900">{portalData?.name}</p>
              </div>
              {portalData?.email && (
                <div>
                  <p className="text-sm font-medium text-gray-500 mb-1">Email</p>
                  <p className="text-base text-gray-900">{portalData.email}</p>
                </div>
              )}
              {portalData?.phoneNumber && (
                <div>
                  <p className="text-sm font-medium text-gray-500 mb-1">Phone</p>
                  <p className="text-base text-gray-900">{portalData.phoneNumber}</p>
                </div>
              )}
            </div>
          </div>
        )}
      </div>

      {/* Redeem Modal */}
      {showRedeemModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 relative">
            <button
              onClick={() => setShowRedeemModal(false)}
              className="absolute top-4 right-4 p-2 hover:bg-gray-100 rounded-full transition-colors"
            >
              <X className="h-5 w-5 text-gray-500" />
            </button>

            <div className="text-center mb-6">
              <div
                className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full"
                style={{ backgroundColor: `${branding.primaryColor}20` }}
              >
                <Wallet className="h-8 w-8" style={{ color: branding.primaryColor }} />
              </div>
              <h2 className="text-2xl font-bold text-gray-900 mb-2">How to Redeem</h2>
              <p className="text-gray-600">Follow these steps at the terminal</p>
            </div>

            <div className="space-y-4 mb-6">
              <div className="flex gap-4">
                <div className="flex-shrink-0 flex h-8 w-8 items-center justify-center rounded-full bg-gray-100 text-sm font-semibold text-gray-700">
                  1
                </div>
                <div>
                  <p className="font-medium text-gray-900">Tap your card to pay</p>
                  <p className="text-sm text-gray-600">Use your registered card at the payment terminal</p>
                </div>
              </div>

              <div className="flex gap-4">
                <div className="flex-shrink-0 flex h-8 w-8 items-center justify-center rounded-full bg-gray-100 text-sm font-semibold text-gray-700">
                  2
                </div>
                <div>
                  <p className="font-medium text-gray-900">Redeem screen appears</p>
                  <p className="text-sm text-gray-600">If you have credit, a redeem screen will automatically appear</p>
                </div>
              </div>

              <div className="flex gap-4">
                <div className="flex-shrink-0 flex h-8 w-8 items-center justify-center rounded-full bg-gray-100 text-sm font-semibold text-gray-700">
                  3
                </div>
                <div>
                  <p className="font-medium text-gray-900">Apply credit to transaction</p>
                  <p className="text-sm text-gray-600">Choose how much credit you want to apply to your purchase</p>
                </div>
              </div>
            </div>

            <Button
              onClick={() => setShowRedeemModal(false)}
              className="w-full h-12 rounded-full font-medium"
              style={{
                backgroundColor: branding.backgroundColor,
                color: branding.textColor
              }}
            >
              Got it
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

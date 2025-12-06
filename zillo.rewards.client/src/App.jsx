import { useState, useEffect } from 'react'
import { Citrus } from 'lucide-react'

function App() {
  const [slug, setSlug] = useState('')
  const [email, setEmail] = useState('')
  const [customer, setCustomer] = useState(null)
  const [transactions, setTransactions] = useState([])
  const [account, setAccount] = useState(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  // Get slug from URL path (e.g., /acme -> slug = acme)
  useEffect(() => {
    const pathSlug = window.location.pathname.substring(1) || 'demo'
    setSlug(pathSlug)

    // Load account info
    fetch(`/api/${pathSlug}/rewards/account`)
      .then(res => res.json())
      .then(data => setAccount(data))
      .catch(() => setError('Rewards program not found'))
  }, [])

  const handleLogin = async (e) => {
    e.preventDefault()
    setError('')
    setLoading(true)

    try {
      const response = await fetch(`/api/${slug}/rewards/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email }),
      })

      if (!response.ok) {
        const errorData = await response.json()
        throw new Error(errorData.message || 'Login failed')
      }

      const data = await response.json()
      setCustomer(data.customer)
      setTransactions(data.transactions)
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  const handleLogout = () => {
    setCustomer(null)
    setTransactions([])
    setEmail('')
  }

  const branding = account?.branding || {
    primaryColor: '#DC2626',
    backgroundColor: '#DC2626',
    textColor: '#FFFFFF',
    buttonColor: '#E5E7EB',
    buttonTextColor: '#1F2937',
    logoUrl: null
  }

  const loyaltySystemType = account?.loyaltySystemType || 'cashback'
  const cashbackRate = account?.cashbackRate || 5.0

  // Helper function to format balance
  const formatBalance = () => {
    if (loyaltySystemType === 'cashback') {
      const cashback = (customer.cashbackBalance || 0) / 100
      return `$${cashback.toFixed(2)}`
    } else {
      return customer.pointsBalance || 0
    }
  }

  // Helper function to get balance label
  const getBalanceLabel = () => {
    return loyaltySystemType === 'cashback' ? 'Available Cash' : 'Your Points Balance'
  }

  if (!account) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gray-50">
        <div className="text-center">
          <Citrus className="h-12 w-12 mx-auto mb-4 text-gray-400" />
          <p className="text-gray-600">Loading rewards program...</p>
        </div>
      </div>
    )
  }

  if (customer) {
    return (
      <div className="min-h-screen py-8 px-4" style={{ backgroundColor: branding.backgroundColor }}>
        <div className="max-w-2xl mx-auto">
          <div className="bg-white rounded-lg shadow-lg overflow-hidden">
            {/* Header */}
            <div className="p-6" style={{ backgroundColor: branding.backgroundColor, color: branding.textColor }}>
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  {branding.logoUrl ? (
                    <img src={branding.logoUrl} alt={account.companyName} className="h-12 w-12 object-contain" />
                  ) : (
                    <Citrus className="h-8 w-8" />
                  )}
                  <div>
                    <h1 className="text-2xl font-bold">{account.companyName}</h1>
                    <p style={{ color: `${branding.textColor}CC` }} className="text-sm">Loyalty Rewards</p>
                  </div>
                </div>
                <button
                  onClick={handleLogout}
                  style={{ color: `${branding.textColor}E6` }}
                  className="text-sm underline hover:opacity-100 opacity-90"
                >
                  Sign Out
                </button>
              </div>
            </div>

            {/* Balance */}
            <div className="p-6 border-b">
              <h2 className="text-lg font-semibold mb-2">Hi {customer.name.split(' ')[0]}!</h2>
              <div className="rounded-lg p-4 text-center" style={{ backgroundColor: `${branding.primaryColor}15` }}>
                <p className="text-sm text-gray-600 mb-1">{getBalanceLabel()} at {account.companyName}</p>
                <p className="text-4xl font-bold" style={{ color: branding.primaryColor }}>{formatBalance()}</p>
              </div>

              {/* Action Buttons */}
              <div className="mt-4 flex gap-3">
                <button
                  className="flex-1 font-semibold py-3 rounded-lg transition-all"
                  style={{
                    backgroundColor: branding.buttonColor,
                    color: branding.buttonTextColor
                  }}
                >
                  Redeem Cash
                </button>
                <button
                  className="flex gap-2 items-center justify-center font-semibold py-3 px-4 rounded-lg transition-all"
                  style={{
                    backgroundColor: `${branding.primaryColor}15`,
                    color: branding.primaryColor
                  }}
                >
                  <span>📷</span> Earn More
                </button>
              </div>
            </div>

            {/* Transactions */}
            <div className="p-6">
              <h3 className="font-semibold mb-4">Activity</h3>
              {transactions.length === 0 ? (
                <p className="text-gray-500 text-center py-8">No transactions yet</p>
              ) : (
                <div className="space-y-3">
                  {transactions.map((transaction) => {
                    const transactionValue = loyaltySystemType === 'cashback'
                      ? `$${((transaction.cashbackAmount || 0) / 100).toFixed(2)}`
                      : transaction.points

                    return (
                      <div
                        key={transaction.id}
                        className="flex items-center justify-between p-4 bg-gray-50 rounded-lg"
                      >
                        <div className="flex-1">
                          <p className="font-medium">{transaction.description}</p>
                          <p className="text-sm text-gray-500">
                            {new Date(transaction.createdAt).toLocaleDateString()}
                          </p>
                        </div>
                        <div className="text-right">
                          <p
                            className={`font-bold ${
                              transaction.type === 'earn' || transaction.type === 'bonus'
                                ? 'text-green-600'
                                : 'text-red-600'
                            }`}
                          >
                            {transaction.type === 'earn' || transaction.type === 'bonus' ? '+' : '-'}
                            {transactionValue}
                          </p>
                          {transaction.amount && (
                            <p className="text-sm text-gray-500">
                              ${(transaction.amount / 100).toFixed(2)}
                            </p>
                          )}
                        </div>
                      </div>
                    )
                  })}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="flex min-h-screen items-center justify-center px-4" style={{ backgroundColor: branding.backgroundColor }}>
      <div className="w-full max-w-md">
        <div className="bg-white rounded-lg shadow-lg overflow-hidden">
          {/* Header */}
          <div className="p-6 text-center" style={{ backgroundColor: branding.backgroundColor, color: branding.textColor }}>
            {branding.logoUrl ? (
              <img src={branding.logoUrl} alt={account.companyName} className="h-16 w-auto mx-auto mb-3 object-contain" />
            ) : (
              <Citrus className="h-12 w-12 mx-auto mb-3" />
            )}
            <h1 className="text-2xl font-bold">{account.companyName}</h1>
            <p style={{ color: `${branding.textColor}CC` }}>Loyalty Rewards</p>
          </div>

          {/* Login Form */}
          <form onSubmit={handleLogin} className="p-6">
            <h2 className="text-xl font-semibold mb-4">Check Your Rewards</h2>

            {error && (
              <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-600">
                {error}
              </div>
            )}

            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium mb-1">Email</label>
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="w-full px-4 py-2 border rounded-lg focus:ring-2 focus:border-transparent"
                  style={{
                    outlineColor: branding.primaryColor,
                  }}
                  placeholder="you@example.com"
                  required
                />
              </div>

              <button
                type="submit"
                disabled={loading}
                className="w-full font-semibold py-3 rounded-lg transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                style={{
                  backgroundColor: branding.buttonColor,
                  color: branding.buttonTextColor
                }}
              >
                {loading ? 'Checking...' : 'Check Balance'}
              </button>
            </div>
          </form>
        </div>

        <p className="text-center text-sm text-white/80 mt-4">
          Enter your email address to view your rewards balance
        </p>
      </div>
    </div>
  )
}

export default App

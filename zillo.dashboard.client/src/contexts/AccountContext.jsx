import { createContext, useContext, useState, useEffect, useCallback } from 'react'
import { useAuth } from './AuthContext'

const AccountContext = createContext({})

export const useAccount = () => useContext(AccountContext)

const SELECTED_ACCOUNT_KEY = 'zillo_selected_account_id'

export const AccountProvider = ({ children }) => {
  const { session, user } = useAuth()
  const [accounts, setAccounts] = useState([])
  const [currentAccount, setCurrentAccount] = useState(null)
  const [loading, setLoading] = useState(true)

  // Load accessible accounts when session changes
  const loadAccounts = useCallback(async () => {
    if (!session?.access_token) {
      setAccounts([])
      setCurrentAccount(null)
      setLoading(false)
      return
    }

    try {
      const response = await fetch('/api/auth/accounts', {
        headers: {
          'Authorization': `Bearer ${session.access_token}`
        }
      })

      if (response.ok) {
        const data = await response.json()
        setAccounts(data.accounts || [])

        // Check localStorage for previously selected account
        const savedAccountId = localStorage.getItem(SELECTED_ACCOUNT_KEY)
        const savedAccount = data.accounts?.find(a => a.id === savedAccountId)

        if (savedAccount) {
          setCurrentAccount(savedAccount)
        } else if (data.accounts?.length > 0) {
          // Default to first account
          setCurrentAccount(data.accounts[0])
          localStorage.setItem(SELECTED_ACCOUNT_KEY, data.accounts[0].id)
        }
      }
    } catch (error) {
      console.error('Failed to load accounts:', error)
    } finally {
      setLoading(false)
    }
  }, [session?.access_token])

  useEffect(() => {
    loadAccounts()
  }, [loadAccounts])

  // Switch to a different account
  const switchAccount = async (accountId) => {
    if (!session?.access_token) return

    try {
      const response = await fetch('/api/auth/switch-account', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${session.access_token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ accountId })
      })

      if (response.ok) {
        const data = await response.json()
        const newAccount = accounts.find(a => a.id === data.accountId)
        if (newAccount) {
          setCurrentAccount(newAccount)
          localStorage.setItem(SELECTED_ACCOUNT_KEY, data.accountId)
        }
        return data
      } else {
        throw new Error('Failed to switch account')
      }
    } catch (error) {
      console.error('Failed to switch account:', error)
      throw error
    }
  }

  // Helper to get auth headers with account context
  const getAuthHeaders = useCallback(() => {
    const headers = {}
    if (session?.access_token) {
      headers['Authorization'] = `Bearer ${session.access_token}`
    }
    if (currentAccount?.id) {
      headers['X-Account-Id'] = currentAccount.id
    }
    return headers
  }, [session?.access_token, currentAccount?.id])

  // Clear selected account on logout
  useEffect(() => {
    if (!user) {
      localStorage.removeItem(SELECTED_ACCOUNT_KEY)
      setAccounts([])
      setCurrentAccount(null)
    }
  }, [user])

  const value = {
    accounts,
    currentAccount,
    loading,
    switchAccount,
    getAuthHeaders,
    refreshAccounts: loadAccounts
  }

  return <AccountContext.Provider value={value}>{children}</AccountContext.Provider>
}

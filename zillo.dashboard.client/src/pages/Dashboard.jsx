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
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Plus, User } from 'lucide-react'

export default function Dashboard() {
  const { user, signOut } = useAuth()
  const navigate = useNavigate()
  const [customers, setCustomers] = useState([])
  const [account, setAccount] = useState(null)
  const [loading, setLoading] = useState(true)
  const [showAddCustomer, setShowAddCustomer] = useState(false)
  const [newCustomer, setNewCustomer] = useState({
    name: '',
    email: '',
    phoneNumber: ''
  })

  useEffect(() => {
    document.title = 'Dashboard | Zillo'
  }, [])

  useEffect(() => {
    if (!user) {
      navigate('/signin')
      return
    }
    loadAccount()
    loadCustomers()
  }, [user, navigate])

  const loadAccount = async () => {
    try {
      const { data, error } = await supabase
        .from('accounts')
        .select('*')
        .eq('supabase_user_id', user.id)
        .single()

      if (error) throw error
      setAccount(data)
    } catch (error) {
      console.error('Error loading account:', error)
    }
  }

  const loadCustomers = async () => {
    try {
      const { data, error } = await supabase
        .from('customers')
        .select('*')
        .order('created_at', { ascending: false })

      if (error) throw error
      setCustomers(data || [])
    } catch (error) {
      console.error('Error loading customers:', error)
    } finally {
      setLoading(false)
    }
  }

  const handleAddCustomer = async (e) => {
    e.preventDefault()
    try {
      const { data: { session } } = await supabase.auth.getSession()
      const { data: accountData } = await supabase
        .from('accounts')
        .select('id')
        .eq('supabase_user_id', session.user.id)
        .single()

      const { error } = await supabase
        .from('customers')
        .insert({
          account_id: accountData.id,
          name: newCustomer.name,
          email: newCustomer.email,
          phone_number: newCustomer.phoneNumber || null,
          points_balance: 0
        })

      if (error) throw error

      setNewCustomer({ name: '', email: '', phoneNumber: '' })
      setShowAddCustomer(false)
      loadCustomers()
    } catch (error) {
      console.error('Error adding customer:', error)
      alert('Failed to add customer: ' + error.message)
    }
  }

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <div className="text-lg text-foreground">Loading...</div>
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
                <BreadcrumbPage>Customers</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
        </header>

        <div className="flex flex-1 flex-col gap-4 p-4 md:p-6">
          <div className="flex items-center justify-between">
            <h2 className="text-xl font-semibold text-foreground">Customers</h2>
            <Button onClick={() => setShowAddCustomer(!showAddCustomer)}>
              <Plus className="mr-2 h-4 w-4" />
              Add Customer
            </Button>
          </div>

          {showAddCustomer && (
            <Card>
              <CardHeader>
                <CardTitle>Add New Customer</CardTitle>
                <CardDescription>Create a new customer profile</CardDescription>
              </CardHeader>
              <CardContent>
                <form onSubmit={handleAddCustomer} className="space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="name">Name</Label>
                    <Input
                      id="name"
                      value={newCustomer.name}
                      onChange={(e) => setNewCustomer({ ...newCustomer, name: e.target.value })}
                      required
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="email">Email</Label>
                    <Input
                      id="email"
                      type="email"
                      value={newCustomer.email}
                      onChange={(e) => setNewCustomer({ ...newCustomer, email: e.target.value })}
                      required
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="phoneNumber">Phone Number (optional)</Label>
                    <Input
                      id="phoneNumber"
                      value={newCustomer.phoneNumber}
                      onChange={(e) => setNewCustomer({ ...newCustomer, phoneNumber: e.target.value })}
                    />
                  </div>
                  <div className="flex gap-2">
                    <Button type="submit">Add Customer</Button>
                    <Button
                      type="button"
                      variant="outline"
                      onClick={() => setShowAddCustomer(false)}
                    >
                      Cancel
                    </Button>
                  </div>
                </form>
              </CardContent>
            </Card>
          )}

          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {customers.map((customer) => (
              <Card key={customer.id} className="cursor-pointer hover:shadow-md transition-shadow">
                <CardHeader>
                  <div className="flex items-start justify-between">
                    <div className="flex items-center gap-2">
                      <User className="h-5 w-5 text-muted-foreground" />
                      <CardTitle className="text-lg">{customer.name}</CardTitle>
                    </div>
                    <div className="text-right">
                      {account?.loyalty_system_type === 'cashback' ? (
                        <>
                          <div className="text-2xl font-bold text-primary">
                            ${(customer.cashback_balance || 0).toFixed(2)}
                          </div>
                          <div className="text-xs text-muted-foreground">cashback</div>
                        </>
                      ) : (
                        <>
                          <div className="text-2xl font-bold text-primary">{customer.points_balance}</div>
                          <div className="text-xs text-muted-foreground">points</div>
                        </>
                      )}
                    </div>
                  </div>
                </CardHeader>
                <CardContent>
                  <div className="space-y-1 text-sm text-muted-foreground">
                    <div>{customer.email}</div>
                    {customer.phone_number && <div>{customer.phone_number}</div>}
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>

          {customers.length === 0 && (
            <Card>
              <CardContent className="py-12 text-center">
                <User className="mx-auto h-12 w-12 text-muted-foreground" />
                <h3 className="mt-4 text-lg font-semibold text-card-foreground">No customers yet</h3>
                <p className="mt-2 text-sm text-muted-foreground">
                  Get started by adding your first customer.
                </p>
              </CardContent>
            </Card>
          )}
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}

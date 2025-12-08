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
import { Switch } from '@/components/ui/switch'
import { Alert, AlertDescription } from '@/components/ui/alert'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Loader2, MapPin, Plus, Pencil, Trash2, CheckCircle2, Search } from 'lucide-react'

export default function Locations() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [locations, setLocations] = useState([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [geocoding, setGeocoding] = useState(false)
  const [success, setSuccess] = useState(false)
  const [error, setError] = useState('')
  const [isDialogOpen, setIsDialogOpen] = useState(false)
  const [editingLocation, setEditingLocation] = useState(null)
  const [deleteConfirmId, setDeleteConfirmId] = useState(null)

  const [formData, setFormData] = useState({
    name: '',
    address: '',
    latitude: '',
    longitude: '',
    relevantDistance: 100,
    isActive: true
  })

  useEffect(() => {
    document.title = 'Locations | Zillo'
  }, [])

  useEffect(() => {
    if (!user) {
      navigate('/signin')
      return
    }
    loadLocations()
  }, [user, navigate])

  const loadLocations = async () => {
    try {
      setLoading(true)
      const { data: { session } } = await supabase.auth.getSession()

      if (!session) {
        navigate('/signin')
        return
      }

      const response = await fetch('/api/locations', {
        headers: {
          'Authorization': `Bearer ${session.access_token}`
        }
      })

      if (response.ok) {
        const data = await response.json()
        setLocations(data)
      }
    } catch (error) {
      console.error('Error loading locations:', error)
      setError('Failed to load locations')
    } finally {
      setLoading(false)
    }
  }

  const resetForm = () => {
    setFormData({
      name: '',
      address: '',
      latitude: '',
      longitude: '',
      relevantDistance: 100,
      isActive: true
    })
    setEditingLocation(null)
  }

  const openCreateDialog = () => {
    resetForm()
    setIsDialogOpen(true)
  }

  const openEditDialog = (location) => {
    setEditingLocation(location)
    setFormData({
      name: location.name,
      address: location.address || '',
      latitude: location.latitude.toString(),
      longitude: location.longitude.toString(),
      relevantDistance: location.relevantDistance || 100,
      isActive: location.isActive
    })
    setIsDialogOpen(true)
  }

  const handleSave = async () => {
    setSaving(true)
    setError('')

    try {
      const { data: { session } } = await supabase.auth.getSession()

      const lat = parseFloat(formData.latitude)
      const lng = parseFloat(formData.longitude)

      if (isNaN(lat) || isNaN(lng)) {
        setError('Please enter valid latitude and longitude values')
        setSaving(false)
        return
      }

      if (lat < -90 || lat > 90) {
        setError('Latitude must be between -90 and 90')
        setSaving(false)
        return
      }

      if (lng < -180 || lng > 180) {
        setError('Longitude must be between -180 and 180')
        setSaving(false)
        return
      }

      const payload = {
        name: formData.name,
        address: formData.address || null,
        latitude: lat,
        longitude: lng,
        relevantDistance: parseFloat(formData.relevantDistance) || 100,
        isActive: formData.isActive
      }

      const url = editingLocation
        ? `/api/locations/${editingLocation.id}`
        : '/api/locations'

      const response = await fetch(url, {
        method: editingLocation ? 'PUT' : 'POST',
        headers: {
          'Authorization': `Bearer ${session.access_token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(payload)
      })

      if (!response.ok) {
        const data = await response.json()
        throw new Error(data.error || 'Failed to save location')
      }

      setIsDialogOpen(false)
      resetForm()
      setSuccess(true)
      setTimeout(() => setSuccess(false), 3000)
      await loadLocations()
    } catch (error) {
      console.error('Error saving location:', error)
      setError(error.message)
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (id) => {
    try {
      const { data: { session } } = await supabase.auth.getSession()

      const response = await fetch(`/api/locations/${id}`, {
        method: 'DELETE',
        headers: {
          'Authorization': `Bearer ${session.access_token}`
        }
      })

      if (!response.ok) {
        const data = await response.json()
        throw new Error(data.error || 'Failed to delete location')
      }

      setDeleteConfirmId(null)
      setSuccess(true)
      setTimeout(() => setSuccess(false), 3000)
      await loadLocations()
    } catch (error) {
      console.error('Error deleting location:', error)
      setError(error.message)
    }
  }

  const handleUseCurrentLocation = () => {
    if (!navigator.geolocation) {
      setError('Geolocation is not supported by your browser')
      return
    }

    navigator.geolocation.getCurrentPosition(
      (position) => {
        setFormData({
          ...formData,
          latitude: position.coords.latitude.toFixed(6),
          longitude: position.coords.longitude.toFixed(6)
        })
      },
      (error) => {
        console.error('Geolocation error:', error)
        setError('Failed to get current location. Please enter coordinates manually.')
      }
    )
  }

  const handleLookupAddress = async () => {
    if (!formData.address.trim()) {
      setError('Please enter an address to look up')
      return
    }

    setGeocoding(true)
    setError('')

    try {
      const { data: { session } } = await supabase.auth.getSession()

      const response = await fetch('/api/locations/geocode', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${session.access_token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ address: formData.address })
      })

      const data = await response.json()

      if (!response.ok) {
        throw new Error(data.error || 'Failed to look up address')
      }

      setFormData({
        ...formData,
        latitude: data.latitude.toFixed(6),
        longitude: data.longitude.toFixed(6),
        address: data.formattedAddress
      })
    } catch (error) {
      console.error('Geocoding error:', error)
      setError(error.message || 'Failed to look up address')
    } finally {
      setGeocoding(false)
    }
  }

  if (loading) {
    return (
      <SidebarProvider>
        <AppSidebar />
        <SidebarInset>
          <div className="flex items-center justify-center h-64">
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          </div>
        </SidebarInset>
      </SidebarProvider>
    )
  }

  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset>
        <header className="flex h-16 shrink-0 items-center gap-2 border-b px-4">
          <SidebarTrigger className="-ml-1" />
          <Separator orientation="vertical" className="mr-2 h-4" />
          <Breadcrumb>
            <BreadcrumbList>
              <BreadcrumbItem className="hidden md:block">
                <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
              </BreadcrumbItem>
              <BreadcrumbSeparator className="hidden md:block" />
              <BreadcrumbItem>
                <BreadcrumbPage>Locations</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
        </header>
        <div className="flex flex-1 flex-col gap-6 p-4 md:p-6">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-2xl font-bold">Store Locations</h1>
              <p className="text-muted-foreground">
                Add your store locations for Apple Wallet geofencing notifications
              </p>
            </div>
            <Button onClick={openCreateDialog}>
              <Plus className="mr-2 h-4 w-4" />
              Add Location
            </Button>
          </div>

          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {success && (
            <Alert>
              <CheckCircle2 className="h-4 w-4" />
              <AlertDescription>Location saved successfully!</AlertDescription>
            </Alert>
          )}

          <div className="border rounded-lg">
            <div className="px-6 py-4 border-b bg-muted/30">
              <div className="flex items-center gap-2">
                <MapPin className="h-5 w-5" />
                <h2 className="text-lg font-semibold">Locations</h2>
              </div>
              <p className="text-sm text-muted-foreground mt-1">
                Customers will receive notifications when they're near these locations (up to 10 locations supported)
              </p>
            </div>
            <div className="divide-y">
              {locations.length === 0 ? (
                <div className="px-6 py-12 text-center text-muted-foreground">
                  <MapPin className="h-12 w-12 mx-auto mb-4 opacity-50" />
                  <p>No locations added yet</p>
                  <p className="text-sm mt-1">Add your first store location to enable geofencing</p>
                </div>
              ) : (
                locations.map((location) => (
                  <div key={location.id} className="px-6 py-4 flex items-center justify-between">
                    <div className="flex items-start gap-4">
                      <div className={`w-10 h-10 rounded-full flex items-center justify-center ${location.isActive ? 'bg-green-100' : 'bg-gray-100'}`}>
                        <MapPin className={`h-5 w-5 ${location.isActive ? 'text-green-600' : 'text-gray-400'}`} />
                      </div>
                      <div>
                        <div className="flex items-center gap-2">
                          <p className="font-medium">{location.name}</p>
                          {!location.isActive && (
                            <span className="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded">Inactive</span>
                          )}
                        </div>
                        {location.address && (
                          <p className="text-sm text-muted-foreground">{location.address}</p>
                        )}
                        <p className="text-xs text-muted-foreground mt-1">
                          {location.latitude.toFixed(6)}, {location.longitude.toFixed(6)} • {location.relevantDistance || 100}m radius
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <Button variant="ghost" size="sm" onClick={() => openEditDialog(location)}>
                        <Pencil className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-red-600 hover:text-red-700 hover:bg-red-50"
                        onClick={() => setDeleteConfirmId(location.id)}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>

          {/* Info box */}
          <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
            <h3 className="font-medium text-blue-900">How it works</h3>
            <ul className="mt-2 text-sm text-blue-800 space-y-1">
              <li>• When customers add your loyalty card to Apple Wallet, their device will monitor for your locations</li>
              <li>• When they're within the specified radius, the pass will appear on their lock screen</li>
              <li>• This helps remind customers to use their rewards when visiting your store</li>
              <li>• Apple Wallet supports up to 10 locations per pass</li>
            </ul>
          </div>
        </div>

        {/* Add/Edit Dialog */}
        <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>{editingLocation ? 'Edit Location' : 'Add Location'}</DialogTitle>
              <DialogDescription>
                {editingLocation
                  ? 'Update this store location for wallet pass geofencing'
                  : 'Add a new store location for wallet pass geofencing'}
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <div className="space-y-2">
                <Label htmlFor="name">Location name *</Label>
                <Input
                  id="name"
                  placeholder="Main Street Store"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="address">Address</Label>
                <div className="flex gap-2">
                  <Input
                    id="address"
                    placeholder="123 Main St, City, State"
                    value={formData.address}
                    onChange={(e) => setFormData({ ...formData, address: e.target.value })}
                    className="flex-1"
                  />
                  <Button
                    type="button"
                    variant="outline"
                    onClick={handleLookupAddress}
                    disabled={geocoding || !formData.address.trim()}
                  >
                    {geocoding ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Search className="h-4 w-4" />
                    )}
                  </Button>
                </div>
                <p className="text-xs text-muted-foreground">
                  Enter an address and click the search button to auto-fill coordinates
                </p>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="latitude">Latitude *</Label>
                  <Input
                    id="latitude"
                    type="number"
                    step="any"
                    placeholder="37.7749"
                    value={formData.latitude}
                    onChange={(e) => setFormData({ ...formData, latitude: e.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="longitude">Longitude *</Label>
                  <Input
                    id="longitude"
                    type="number"
                    step="any"
                    placeholder="-122.4194"
                    value={formData.longitude}
                    onChange={(e) => setFormData({ ...formData, longitude: e.target.value })}
                  />
                </div>
              </div>
              <Button
                type="button"
                variant="outline"
                className="w-full"
                onClick={handleUseCurrentLocation}
              >
                <MapPin className="mr-2 h-4 w-4" />
                Use Current Location
              </Button>
              <div className="space-y-2">
                <Label htmlFor="relevantDistance">Notification radius (meters)</Label>
                <Input
                  id="relevantDistance"
                  type="number"
                  min="10"
                  max="1000"
                  placeholder="100"
                  value={formData.relevantDistance}
                  onChange={(e) => setFormData({ ...formData, relevantDistance: e.target.value })}
                />
                <p className="text-xs text-muted-foreground">
                  How close customers need to be to trigger a notification (default: 100m)
                </p>
              </div>
              <div className="flex items-center justify-between">
                <div className="space-y-0.5">
                  <Label htmlFor="isActive">Active</Label>
                  <p className="text-sm text-muted-foreground">
                    Include this location in wallet passes
                  </p>
                </div>
                <Switch
                  id="isActive"
                  checked={formData.isActive}
                  onCheckedChange={(checked) => setFormData({ ...formData, isActive: checked })}
                />
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleSave} disabled={saving || !formData.name || !formData.latitude || !formData.longitude}>
                {saving ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Saving...
                  </>
                ) : (
                  'Save'
                )}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Delete Confirmation Dialog */}
        <Dialog open={!!deleteConfirmId} onOpenChange={() => setDeleteConfirmId(null)}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Delete Location</DialogTitle>
              <DialogDescription>
                Are you sure you want to delete this location? This action cannot be undone.
              </DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <Button variant="outline" onClick={() => setDeleteConfirmId(null)}>
                Cancel
              </Button>
              <Button variant="destructive" onClick={() => handleDelete(deleteConfirmId)}>
                Delete
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </SidebarInset>
    </SidebarProvider>
  )
}

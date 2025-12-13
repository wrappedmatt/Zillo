import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider } from './contexts/AuthContext'
import { AccountProvider } from './contexts/AccountContext'
import SignIn from './pages/SignIn'
import SignUp from './pages/SignUp'
import Dashboard from './pages/Dashboard'
import Terminals from './pages/Terminals'
import Customers from './pages/Customers'
import CustomerDetails from './pages/CustomerDetails'
import Transactions from './pages/Transactions'
import Settings from './pages/Settings'
import Locations from './pages/Locations'
import Reporting from './pages/Reporting'

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AccountProvider>
          <Routes>
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/signin" element={<SignIn />} />
          <Route path="/signup" element={<SignUp />} />
          <Route path="/dashboard" element={<Dashboard />} />
          <Route path="/terminals" element={<Terminals />} />

          {/* Admin Customer Routes */}
          <Route path="/customers" element={<Customers />} />
          <Route path="/customers/:customerId" element={<CustomerDetails />} />

          {/* Admin Transactions */}
          <Route path="/transactions" element={<Transactions />} />

          {/* Admin Settings, Locations & Reporting */}
          <Route path="/settings" element={<Settings />} />
          <Route path="/locations" element={<Locations />} />
          <Route path="/reporting" element={<Reporting />} />
          </Routes>
        </AccountProvider>
      </AuthProvider>
    </BrowserRouter>
  )
}

export default App

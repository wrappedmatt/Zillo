import { BrowserRouter, Routes, Route } from 'react-router-dom'
import CustomerSignup from './pages/CustomerSignup'
import CustomerPortal from './pages/CustomerPortal'
import RewardsLookup from './pages/RewardsLookup'

function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Customer Portal Routes */}
        <Route path="/signup/:slug" element={<CustomerSignup />} />
        <Route path="/portal/:token" element={<CustomerPortal />} />

        {/* Legacy email-based rewards lookup */}
        <Route path="/:slug" element={<RewardsLookup />} />
        <Route path="/" element={<RewardsLookup />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App

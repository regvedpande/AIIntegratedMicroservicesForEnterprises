import { lazy, Suspense } from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { Box, CircularProgress } from '@mui/material'
import { useAuthStore } from './store/auth'
import Layout from './components/Layout'
import Login from './pages/Login'

const Dashboard = lazy(() => import('./pages/Dashboard'))
const Compliance = lazy(() => import('./pages/Compliance'))
const Documents = lazy(() => import('./pages/Documents'))
const Vendors = lazy(() => import('./pages/Vendors'))
const AuditLog = lazy(() => import('./pages/AuditLog'))
const Alerts = lazy(() => import('./pages/Alerts'))
const SystemHealth = lazy(() => import('./pages/SystemHealth'))

function PageLoader() {
  return (
    <Box sx={{ minHeight: 240, display: 'grid', placeItems: 'center' }} role="status" aria-label="Loading page">
      <CircularProgress size={32} />
    </Box>
  )
}

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const user = useAuthStore((s) => s.user)
  if (!user) return <Navigate to="/login" replace />
  return <>{children}</>
}

export default function App() {
  const user = useAuthStore((s) => s.user)

  return (
    <BrowserRouter future={{ v7_relativeSplatPath: true }}>
      <Routes>
        <Route
          path="/login"
          element={user ? <Navigate to="/" replace /> : <Login />}
        />
        <Route
          path="/"
          element={
            <ProtectedRoute>
              <Layout />
            </ProtectedRoute>
          }
        >
          <Route index element={<Suspense fallback={<PageLoader />}><Dashboard /></Suspense>} />
          <Route path="compliance" element={<Suspense fallback={<PageLoader />}><Compliance /></Suspense>} />
          <Route path="documents" element={<Suspense fallback={<PageLoader />}><Documents /></Suspense>} />
          <Route path="vendors" element={<Suspense fallback={<PageLoader />}><Vendors /></Suspense>} />
          <Route path="health" element={<Suspense fallback={<PageLoader />}><SystemHealth /></Suspense>} />
          <Route path="audit" element={<Suspense fallback={<PageLoader />}><AuditLog /></Suspense>} />
          <Route path="alerts" element={<Suspense fallback={<PageLoader />}><Alerts /></Suspense>} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

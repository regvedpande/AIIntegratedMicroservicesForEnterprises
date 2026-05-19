import axios from 'axios'
import { useAuthStore } from '../store/auth'

const API_KEY = import.meta.env.VITE_GATEWAY_API_KEY || 'dev-gateway-api-key-local-testing-only'

export const apiClient = axios.create({
  baseURL: '/api/gateway',
  headers: {
    'Content-Type': 'application/json',
    'X-Api-Key': API_KEY,
  },
  timeout: 300000, // 5 minutes — NVIDIA AI analysis can take 60-120 seconds
})

// Request interceptor: attach Bearer token, auto-logout if expired
apiClient.interceptors.request.use(
  (config) => {
    const user = useAuthStore.getState().user
    if (user?.token) {
      // Check token expiry before sending
      try {
        const payload = JSON.parse(atob(user.token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')))
        if (payload.exp && payload.exp < Math.floor(Date.now() / 1000)) {
          useAuthStore.getState().logout()
          return Promise.reject(new axios.Cancel('Session expired. Please log in again.'))
        }
      } catch { /* malformed token — let server reject it */ }
      config.headers.Authorization = `Bearer ${user.token}`
    }
    return config
  },
  (error) => Promise.reject(error)
)

// Response interceptor: propagate errors to callers
apiClient.interceptors.response.use(
  (response) => response,
  (error) => Promise.reject(error)
)

export function getErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data
    if (typeof data === 'string') return data
    if (data?.message) return data.message
    if (data?.title) return data.title
    if (data?.errors) {
      const msgs = Object.values(data.errors).flat()
      return msgs.join(', ')
    }
    return error.message
  }
  if (error instanceof Error) return error.message
  return 'An unexpected error occurred.'
}

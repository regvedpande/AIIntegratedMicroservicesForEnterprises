import axios from 'axios'
import { useAuthStore } from '../store/auth'

const API_KEY = import.meta.env.VITE_GATEWAY_API_KEY ?? 'REPLACE_WITH_YOUR_GATEWAY_API_KEY'

export const apiClient = axios.create({
  baseURL: '/api/gateway',
  headers: {
    'Content-Type': 'application/json',
    'X-Api-Key': API_KEY,
  },
  timeout: 30000,
})

// Request interceptor: attach Bearer token
apiClient.interceptors.request.use(
  (config) => {
    const user = useAuthStore.getState().user
    if (user?.token) {
      config.headers.Authorization = `Bearer ${user.token}`
    }
    return config
  },
  (error) => Promise.reject(error)
)

// Response interceptor: handle 401 → logout
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      useAuthStore.getState().logout()
    }
    return Promise.reject(error)
  }
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

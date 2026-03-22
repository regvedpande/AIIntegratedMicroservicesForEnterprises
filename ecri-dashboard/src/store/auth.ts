import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { SignJWT } from 'jose'
import type { AuthUser } from '../types'

const JWT_SECRET = 'dev-local-testing-jwt-secret-key-32chars!!'
const DEMO_ENTERPRISE_ID = '00000000-0000-0000-0000-000000000001'
const DEMO_USER_ID = '00000000-0000-0000-0000-000000000001'

interface AuthState {
  user: AuthUser | null
  isLoading: boolean
  error: string | null
  login: (email: string, password: string) => Promise<void>
  logout: () => void
  clearError: () => void
}

async function generateToken(email: string, userId: string, enterpriseId: string): Promise<string> {
  const secret = new TextEncoder().encode(JWT_SECRET)
  const token = await new SignJWT({
    sub: userId,
    enterprise_id: enterpriseId,
    role: 'Admin',
    email,
  })
    .setProtectedHeader({ alg: 'HS256' })
    .setIssuer('AiEnterpriseGateway')
    .setAudience('AiEnterprise')
    .setIssuedAt()
    .setExpirationTime('8h')
    .sign(secret)
  return token
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      isLoading: false,
      error: null,

      login: async (email: string, _password: string) => {
        set({ isLoading: true, error: null })
        try {
          if (!email || !email.includes('@')) {
            throw new Error('Please enter a valid email address.')
          }
          const token = await generateToken(email, DEMO_USER_ID, DEMO_ENTERPRISE_ID)
          const user: AuthUser = {
            userId: DEMO_USER_ID,
            enterpriseId: DEMO_ENTERPRISE_ID,
            email,
            role: 'Admin',
            token,
          }
          set({ user, isLoading: false, error: null })
        } catch (err) {
          const message = err instanceof Error ? err.message : 'Authentication failed.'
          set({ isLoading: false, error: message })
          throw err
        }
      },

      logout: () => {
        set({ user: null, error: null })
      },

      clearError: () => {
        set({ error: null })
      },
    }),
    {
      name: 'ecri-auth',
      partialize: (state) => ({ user: state.user }),
    }
  )
)

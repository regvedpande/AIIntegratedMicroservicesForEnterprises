import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Box,
  Paper,
  Typography,
  TextField,
  Button,
  InputAdornment,
  IconButton,
  Alert,
  Divider,
  CircularProgress,
} from '@mui/material'
import {
  Visibility,
  VisibilityOff,
  Security as SecurityIcon,
  Email as EmailIcon,
  Lock as LockIcon,
  FlashOn as FlashIcon,
} from '@mui/icons-material'
import { alpha } from '@mui/material/styles'
import { useAuthStore } from '../store/auth'

const DEMO_EMAIL = 'admin@enterprise.local'
const DEMO_PASSWORD = 'Demo@1234'

export default function Login() {
  const navigate = useNavigate()
  const { login, isLoading, error, clearError } = useAuthStore()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [validationErrors, setValidationErrors] = useState<{ email?: string; password?: string }>({})

  function validate(): boolean {
    const errors: typeof validationErrors = {}
    if (!email) errors.email = 'Email is required'
    else if (!/\S+@\S+\.\S+/.test(email)) errors.email = 'Enter a valid email address'
    if (!password) errors.password = 'Password is required'
    setValidationErrors(errors)
    return Object.keys(errors).length === 0
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    clearError()
    if (!validate()) return
    try {
      await login(email, password)
      navigate('/')
    } catch {
      // error is set in store
    }
  }

  function handleDemoAccess() {
    clearError()
    setEmail(DEMO_EMAIL)
    setPassword(DEMO_PASSWORD)
    setValidationErrors({})
  }

  async function handleDemoLogin() {
    clearError()
    try {
      await login(DEMO_EMAIL, DEMO_PASSWORD)
      navigate('/')
    } catch {
      // error is set in store
    }
  }

  return (
    <Box
      sx={{
        display: 'flex',
        height: '100vh',
        width: '100vw',
        overflow: 'hidden',
        backgroundColor: '#0D1117',
      }}
    >
      {/* Left panel */}
      <Box
        sx={{
          flex: '0 0 50%',
          display: { xs: 'none', md: 'flex' },
          flexDirection: 'column',
          justifyContent: 'space-between',
          p: 6,
          background: 'linear-gradient(160deg, #161B22 0%, #0D1117 60%, #0D1117 100%)',
          borderRight: '1px solid #30363D',
          position: 'relative',
          overflow: 'hidden',
        }}
      >
        {/* Background grid pattern */}
        <Box
          sx={{
            position: 'absolute',
            inset: 0,
            backgroundImage: `
              linear-gradient(rgba(47, 129, 247, 0.03) 1px, transparent 1px),
              linear-gradient(90deg, rgba(47, 129, 247, 0.03) 1px, transparent 1px)
            `,
            backgroundSize: '40px 40px',
            pointerEvents: 'none',
          }}
        />

        {/* Glow orb */}
        <Box
          sx={{
            position: 'absolute',
            top: '30%',
            left: '20%',
            width: 400,
            height: 400,
            borderRadius: '50%',
            background: 'radial-gradient(circle, rgba(47, 129, 247, 0.08) 0%, transparent 70%)',
            pointerEvents: 'none',
          }}
        />

        {/* Logo */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, zIndex: 1 }}>
          <Box
            sx={{
              width: 44,
              height: 44,
              borderRadius: 2,
              background: 'linear-gradient(135deg, #2F81F7 0%, #1F6FEB 100%)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              boxShadow: `0 0 24px ${alpha('#2F81F7', 0.4)}`,
            }}
          >
            <SecurityIcon sx={{ fontSize: 24, color: '#fff' }} />
          </Box>
          <Box>
            <Typography variant="h6" sx={{ fontWeight: 700, lineHeight: 1, letterSpacing: '-0.01em' }}>
              ECRI
            </Typography>
            <Typography variant="caption" sx={{ color: 'text.secondary', fontSize: '0.65rem', letterSpacing: '0.08em' }}>
              ENTERPRISE PLATFORM
            </Typography>
          </Box>
        </Box>

        {/* Hero text */}
        <Box sx={{ zIndex: 1 }}>
          <Typography
            variant="h2"
            sx={{
              fontWeight: 700,
              fontSize: '2.5rem',
              lineHeight: 1.15,
              mb: 2,
              letterSpacing: '-0.02em',
            }}
          >
            AI-Powered
            <br />
            <Box component="span" sx={{ color: '#2F81F7' }}>
              Compliance
            </Box>
            {' & Risk'}
            <br />
            Intelligence
          </Typography>
          <Typography variant="body1" sx={{ color: 'text.secondary', lineHeight: 1.7, maxWidth: 380 }}>
            Monitor regulatory compliance, assess vendor risk, and detect behavioral anomalies
            across your enterprise — powered by AI.
          </Typography>

          {/* Feature list */}
          <Box sx={{ mt: 4, display: 'flex', flexDirection: 'column', gap: 2 }}>
            {[
              { label: 'GDPR, SOX, HIPAA, PCI DSS compliance monitoring' },
              { label: 'AI document risk analysis in seconds' },
              { label: 'Real-time behavioral anomaly detection' },
              { label: 'Vendor risk scoring & continuous assessment' },
            ].map((item) => (
              <Box key={item.label} sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
                <Box
                  sx={{
                    width: 6,
                    height: 6,
                    borderRadius: '50%',
                    backgroundColor: '#2F81F7',
                    flexShrink: 0,
                  }}
                />
                <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                  {item.label}
                </Typography>
              </Box>
            ))}
          </Box>
        </Box>

        {/* Bottom text */}
        <Box sx={{ zIndex: 1 }}>
          <Typography variant="caption" sx={{ color: '#30363D', fontSize: '0.72rem' }}>
            Enterprise Compliance & Risk Intelligence Platform v1.0
          </Typography>
        </Box>
      </Box>

      {/* Right panel – login form */}
      <Box
        sx={{
          flex: 1,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          p: 4,
        }}
      >
        <Box sx={{ width: '100%', maxWidth: 400 }}>
          {/* Mobile logo */}
          <Box sx={{ display: { xs: 'flex', md: 'none' }, alignItems: 'center', gap: 1.5, mb: 4 }}>
            <Box
              sx={{
                width: 36,
                height: 36,
                borderRadius: 1.5,
                background: 'linear-gradient(135deg, #2F81F7 0%, #1F6FEB 100%)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <SecurityIcon sx={{ fontSize: 20, color: '#fff' }} />
            </Box>
            <Typography variant="h6" sx={{ fontWeight: 700 }}>ECRI</Typography>
          </Box>

          <Typography variant="h5" sx={{ fontWeight: 700, mb: 0.75, letterSpacing: '-0.01em' }}>
            Sign in to your workspace
          </Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary', mb: 4 }}>
            Enter your credentials or use Demo Access to explore the platform.
          </Typography>

          {error && (
            <Alert severity="error" sx={{ mb: 2, fontSize: '0.85rem' }} onClose={clearError}>
              {error}
            </Alert>
          )}

          <Box component="form" onSubmit={handleSubmit} noValidate sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <TextField
              label="Email address"
              type="email"
              value={email}
              onChange={(e) => { setEmail(e.target.value); setValidationErrors((v) => ({ ...v, email: undefined })) }}
              error={!!validationErrors.email}
              helperText={validationErrors.email}
              fullWidth
              autoComplete="email"
              autoFocus
              InputProps={{
                startAdornment: (
                  <InputAdornment position="start">
                    <EmailIcon sx={{ fontSize: 18, color: 'text.secondary' }} />
                  </InputAdornment>
                ),
              }}
            />

            <TextField
              label="Password"
              type={showPassword ? 'text' : 'password'}
              value={password}
              onChange={(e) => { setPassword(e.target.value); setValidationErrors((v) => ({ ...v, password: undefined })) }}
              error={!!validationErrors.password}
              helperText={validationErrors.password}
              fullWidth
              autoComplete="current-password"
              InputProps={{
                startAdornment: (
                  <InputAdornment position="start">
                    <LockIcon sx={{ fontSize: 18, color: 'text.secondary' }} />
                  </InputAdornment>
                ),
                endAdornment: (
                  <InputAdornment position="end">
                    <IconButton
                      size="small"
                      onClick={() => setShowPassword(!showPassword)}
                      edge="end"
                      tabIndex={-1}
                    >
                      {showPassword ? <VisibilityOff fontSize="small" /> : <Visibility fontSize="small" />}
                    </IconButton>
                  </InputAdornment>
                ),
              }}
            />

            <Button
              type="submit"
              variant="contained"
              fullWidth
              disabled={isLoading}
              sx={{
                py: 1.25,
                fontSize: '0.95rem',
                background: 'linear-gradient(135deg, #2F81F7 0%, #1F6FEB 100%)',
                '&:hover': { background: 'linear-gradient(135deg, #58A6FF 0%, #2F81F7 100%)' },
              }}
            >
              {isLoading ? <CircularProgress size={20} color="inherit" /> : 'Sign In'}
            </Button>
          </Box>

          <Divider sx={{ my: 3 }}>
            <Typography variant="caption" sx={{ color: 'text.secondary', px: 1 }}>
              OR
            </Typography>
          </Divider>

          {/* Demo access */}
          <Paper
            sx={{
              p: 2.5,
              border: '1px solid',
              borderColor: alpha('#2F81F7', 0.3),
              backgroundColor: alpha('#2F81F7', 0.04),
            }}
          >
            <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1.5, mb: 2 }}>
              <FlashIcon sx={{ color: '#2F81F7', fontSize: 18, mt: 0.25 }} />
              <Box>
                <Typography variant="subtitle2" sx={{ fontWeight: 600, color: 'text.primary', mb: 0.25 }}>
                  Demo Access
                </Typography>
                <Typography variant="caption" sx={{ color: 'text.secondary', lineHeight: 1.5 }}>
                  Explore the full platform with pre-configured enterprise data. No setup required.
                </Typography>
              </Box>
            </Box>

            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.75, mb: 2 }}>
              {[
                { label: 'Email', value: DEMO_EMAIL },
                { label: 'Password', value: DEMO_PASSWORD },
              ].map(({ label, value }) => (
                <Box
                  key={label}
                  sx={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    px: 1.5,
                    py: 0.75,
                    borderRadius: 1,
                    backgroundColor: '#0D1117',
                    border: '1px solid #30363D',
                  }}
                >
                  <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                    {label}
                  </Typography>
                  <Typography
                    variant="caption"
                    sx={{ color: 'text.primary', fontWeight: 500, fontFamily: 'monospace' }}
                  >
                    {value}
                  </Typography>
                </Box>
              ))}
            </Box>

            <Box sx={{ display: 'flex', gap: 1 }}>
              <Button
                variant="outlined"
                size="small"
                onClick={handleDemoAccess}
                sx={{ flex: 1, fontSize: '0.8rem' }}
              >
                Fill Credentials
              </Button>
              <Button
                variant="contained"
                size="small"
                onClick={handleDemoLogin}
                disabled={isLoading}
                sx={{ flex: 1, fontSize: '0.8rem' }}
              >
                {isLoading ? <CircularProgress size={14} color="inherit" /> : 'Quick Access'}
              </Button>
            </Box>
          </Paper>
        </Box>
      </Box>
    </Box>
  )
}

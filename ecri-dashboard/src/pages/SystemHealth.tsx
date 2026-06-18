import { useEffect } from 'react'
import { useOutletContext } from 'react-router-dom'
import {
  Box,
  Grid,
  Paper,
  Typography,
  Chip,
  Skeleton,
} from '@mui/material'
import {
  CheckCircle as HealthyIcon,
  WarningAmber as DegradedIcon,
  ErrorOutline as UnreachableIcon,
} from '@mui/icons-material'
import { alpha } from '@mui/material/styles'
import { format } from 'date-fns'
import { useQuery } from '@tanstack/react-query'
import { healthApi } from '../api/endpoints'
import { getErrorMessage } from '../api/client'

type OutletCtx = { setSnackbar: (s: { open: boolean; message: string; severity: 'success' | 'error' }) => void }

const STATUS_STYLES: Record<string, { color: string; icon: typeof HealthyIcon }> = {
  Healthy: { color: '#3FB950', icon: HealthyIcon },
  Degraded: { color: '#D29922', icon: DegradedIcon },
  Unreachable: { color: '#F85149', icon: UnreachableIcon },
}

function getStatusStyle(status: string) {
  return STATUS_STYLES[status] ?? STATUS_STYLES.Degraded
}

export default function SystemHealth() {
  const { setSnackbar } = useOutletContext<OutletCtx>()

  const health = useQuery({
    queryKey: ['system-health'],
    queryFn: () => healthApi.check(),
    refetchInterval: 15_000,
  })

  useEffect(() => {
    if (health.error) {
      setSnackbar({ open: true, message: getErrorMessage(health.error), severity: 'error' })
    }
  }, [health.error, setSnackbar])

  const services = Object.entries(health.data?.services ?? {})

  return (
    <Box>
      <Box sx={{ mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700, letterSpacing: '-0.01em' }}>
          System Health
        </Typography>
        <Typography variant="body2" sx={{ color: 'text.secondary', mt: 0.5 }}>
          Live status for the gateway and each downstream microservice
        </Typography>
      </Box>

      <Paper
        sx={{
          p: 3,
          mb: 3,
          border: '1px solid #30363D',
          background: 'linear-gradient(135deg, rgba(47,129,247,0.12) 0%, rgba(22,27,34,0.9) 100%)',
        }}
      >
        {health.isLoading ? (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
            <Skeleton width={180} height={36} />
            <Skeleton width={260} height={20} />
          </Box>
        ) : (
          <>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
              <Typography variant="h4" sx={{ fontWeight: 800 }}>
                {health.data?.overallStatus ?? 'Unknown'}
              </Typography>
              <Chip
                label={`Gateway: ${health.data?.gateway ?? 'Unknown'}`}
                sx={{
                  fontWeight: 700,
                  color: '#E6EDF3',
                  backgroundColor: alpha(getStatusStyle(health.data?.gateway ?? 'Degraded').color, 0.18),
                  border: '1px solid',
                  borderColor: alpha(getStatusStyle(health.data?.gateway ?? 'Degraded').color, 0.45),
                }}
              />
            </Box>
            <Typography variant="body2" sx={{ color: 'text.secondary', mt: 1 }}>
              Last checked {health.data?.checkedAt ? format(new Date(health.data.checkedAt), 'MMM d, yyyy h:mm:ss a') : 'just now'}
            </Typography>
          </>
        )}
      </Paper>

      <Grid container spacing={2}>
        {health.isLoading
          ? [...Array(5)].map((_, index) => (
              <Grid item xs={12} md={6} lg={4} key={index}>
                <Paper sx={{ p: 2.5, border: '1px solid #21262D' }}>
                  <Skeleton width={140} height={24} />
                  <Skeleton width={110} height={32} sx={{ mt: 1 }} />
                </Paper>
              </Grid>
            ))
          : services.map(([name, status]) => {
              const style = getStatusStyle(status)
              const StatusIcon = style.icon

              return (
                <Grid item xs={12} md={6} lg={4} key={name}>
                  <Paper
                    sx={{
                      p: 2.5,
                      height: '100%',
                      border: '1px solid #21262D',
                      backgroundColor: alpha(style.color, 0.08),
                    }}
                  >
                    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2 }}>
                      <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                        {name}
                      </Typography>
                      <StatusIcon sx={{ color: style.color }} />
                    </Box>
                    <Typography variant="h6" sx={{ mt: 2, fontWeight: 700, color: style.color }}>
                      {status}
                    </Typography>
                    <Typography variant="body2" sx={{ mt: 1, color: 'text.secondary' }}>
                      {status === 'Healthy'
                        ? 'Service is responding normally through the gateway.'
                        : status === 'Unreachable'
                          ? 'The gateway could not connect to this service.'
                          : 'The service responded, but reported a degraded state.'}
                    </Typography>
                  </Paper>
                </Grid>
              )
            })}
      </Grid>
    </Box>
  )
}

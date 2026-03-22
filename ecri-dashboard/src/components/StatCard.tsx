import { Box, Paper, Typography, Skeleton } from '@mui/material'
import type { SvgIconComponent } from '@mui/icons-material'
import { alpha } from '@mui/material/styles'

interface Props {
  title: string
  value: number | string
  subtitle?: string
  icon: SvgIconComponent
  color?: string
  loading?: boolean
  trend?: { value: number; label: string }
}

export default function StatCard({ title, value, subtitle, icon: Icon, color = '#2F81F7', loading = false, trend }: Props) {
  return (
    <Paper
      sx={{
        p: 2.5,
        display: 'flex',
        flexDirection: 'column',
        gap: 1.5,
        position: 'relative',
        overflow: 'hidden',
        '&::before': {
          content: '""',
          position: 'absolute',
          top: 0,
          left: 0,
          right: 0,
          height: 2,
          background: color,
          opacity: 0.8,
        },
      }}
    >
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <Typography
          variant="caption"
          sx={{
            color: 'text.secondary',
            textTransform: 'uppercase',
            letterSpacing: '0.06em',
            fontWeight: 600,
            fontSize: '0.7rem',
          }}
        >
          {title}
        </Typography>
        <Box
          sx={{
            p: 0.75,
            borderRadius: 1.5,
            backgroundColor: alpha(color, 0.12),
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Icon sx={{ fontSize: 18, color }} />
        </Box>
      </Box>

      {loading ? (
        <>
          <Skeleton variant="text" width="60%" height={44} />
          <Skeleton variant="text" width="40%" height={20} />
        </>
      ) : (
        <>
          <Typography
            variant="h4"
            sx={{
              fontWeight: 700,
              fontSize: '2rem',
              lineHeight: 1,
              color: 'text.primary',
              letterSpacing: '-0.02em',
            }}
          >
            {value}
          </Typography>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            {trend && (
              <Typography
                variant="caption"
                sx={{
                  color: trend.value >= 0 ? '#3FB950' : '#F85149',
                  fontWeight: 600,
                  fontSize: '0.75rem',
                }}
              >
                {trend.value >= 0 ? '+' : ''}{trend.value}%
              </Typography>
            )}
            {subtitle && (
              <Typography variant="caption" sx={{ color: 'text.secondary', fontSize: '0.75rem' }}>
                {subtitle}
              </Typography>
            )}
          </Box>
        </>
      )}
    </Paper>
  )
}

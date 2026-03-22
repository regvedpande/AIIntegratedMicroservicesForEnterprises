import { Chip, Box, Typography } from '@mui/material'
import { alpha } from '@mui/material/styles'
import { RISK_LEVEL_NAMES } from '../types'
import { RISK_LEVEL_COLORS } from '../theme'

interface Props {
  riskLevel: number
  score?: number
  variant?: 'chip' | 'gauge' | 'inline'
}

export default function RiskBadge({ riskLevel, score, variant = 'chip' }: Props) {
  const name = RISK_LEVEL_NAMES[riskLevel] ?? 'Unknown'
  const color =
    RISK_LEVEL_COLORS[riskLevel as keyof typeof RISK_LEVEL_COLORS] ?? '#8B949E'

  if (variant === 'gauge' && score !== undefined) {
    const pct = Math.min(100, Math.max(0, score))
    const circumference = 2 * Math.PI * 36
    const offset = circumference - (pct / 100) * circumference

    return (
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 0.5 }}>
        <Box sx={{ position: 'relative', width: 88, height: 88 }}>
          <svg width={88} height={88} style={{ transform: 'rotate(-90deg)' }}>
            <circle cx={44} cy={44} r={36} fill="none" stroke="#21262D" strokeWidth={8} />
            <circle
              cx={44}
              cy={44}
              r={36}
              fill="none"
              stroke={color}
              strokeWidth={8}
              strokeDasharray={circumference}
              strokeDashoffset={offset}
              strokeLinecap="round"
            />
          </svg>
          <Box
            sx={{
              position: 'absolute',
              inset: 0,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Typography variant="h6" sx={{ fontSize: '1.1rem', fontWeight: 700, color, lineHeight: 1 }}>
              {Math.round(pct)}
            </Typography>
          </Box>
        </Box>
        <Chip
          label={name}
          size="small"
          sx={{
            color,
            backgroundColor: alpha(color, 0.15),
            border: `1px solid ${alpha(color, 0.35)}`,
            fontWeight: 600,
            fontSize: '0.68rem',
            textTransform: 'uppercase',
            letterSpacing: '0.04em',
          }}
        />
      </Box>
    )
  }

  if (variant === 'inline') {
    return (
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
        <Box
          sx={{
            width: 8,
            height: 8,
            borderRadius: '50%',
            backgroundColor: color,
            flexShrink: 0,
          }}
        />
        <Typography variant="body2" sx={{ color, fontWeight: 500 }}>
          {name}
        </Typography>
      </Box>
    )
  }

  return (
    <Chip
      label={name}
      size="small"
      sx={{
        color,
        backgroundColor: alpha(color, 0.15),
        border: `1px solid ${alpha(color, 0.35)}`,
        fontWeight: 600,
        fontSize: '0.7rem',
        textTransform: 'uppercase',
        letterSpacing: '0.03em',
      }}
    />
  )
}

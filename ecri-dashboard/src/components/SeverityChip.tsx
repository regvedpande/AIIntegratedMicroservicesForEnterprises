import { Chip } from '@mui/material'
import { alpha } from '@mui/material/styles'
import { SEVERITY_NAMES } from '../types'
import { SEVERITY_COLORS } from '../theme'

interface Props {
  severity: number | string
  size?: 'small' | 'medium'
}

export default function SeverityChip({ severity, size = 'small' }: Props) {
  const name =
    typeof severity === 'number'
      ? SEVERITY_NAMES[severity] ?? String(severity)
      : severity
  const color =
    typeof severity === 'number'
      ? SEVERITY_COLORS[severity as keyof typeof SEVERITY_COLORS] ?? '#8B949E'
      : SEVERITY_COLORS[severity as keyof typeof SEVERITY_COLORS] ?? '#8B949E'

  return (
    <Chip
      label={name}
      size={size}
      sx={{
        color,
        backgroundColor: alpha(color, 0.15),
        border: `1px solid ${alpha(color, 0.35)}`,
        fontWeight: 600,
        fontSize: '0.7rem',
        letterSpacing: '0.03em',
        textTransform: 'uppercase',
      }}
    />
  )
}

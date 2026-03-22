import { Box, Typography } from '@mui/material'
import { alpha } from '@mui/material/styles'
import { FRAMEWORK_NAMES } from '../types'

const FRAMEWORK_COLORS: Record<number, string> = {
  0: '#2F81F7', // GDPR – blue
  1: '#A371F7', // SOX – purple
  2: '#3FB950', // HIPAA – green
  3: '#F85149', // PCI DSS – red
  4: '#D29922', // ISO 27001 – gold
  5: '#39D353', // CCPA – teal-green
  6: '#58A6FF', // NIST – light blue
}

interface Props {
  framework: number
  size?: 'small' | 'medium'
}

export default function FrameworkLabel({ framework, size = 'small' }: Props) {
  const name = FRAMEWORK_NAMES[framework] ?? 'Unknown'
  const color = FRAMEWORK_COLORS[framework] ?? '#8B949E'

  return (
    <Box
      sx={{
        display: 'inline-flex',
        alignItems: 'center',
        px: size === 'small' ? 1 : 1.5,
        py: size === 'small' ? 0.25 : 0.5,
        borderRadius: 1,
        backgroundColor: alpha(color, 0.12),
        border: `1px solid ${alpha(color, 0.3)}`,
      }}
    >
      <Typography
        sx={{
          color,
          fontWeight: 600,
          fontSize: size === 'small' ? '0.68rem' : '0.8rem',
          letterSpacing: '0.04em',
          textTransform: 'uppercase',
        }}
      >
        {name}
      </Typography>
    </Box>
  )
}

import { useState, useEffect } from 'react'
import { useOutletContext } from 'react-router-dom'
import {
  Box,
  Paper,
  Typography,
  Button,
  Chip,
  Skeleton,
  IconButton,
  Tooltip,
} from '@mui/material'
import {
  Notifications as NotificationsIcon,
  CheckCircle as AckIcon,
  NotificationsOff as SilencedIcon,
} from '@mui/icons-material'
import { alpha } from '@mui/material/styles'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { format } from 'date-fns'
import { useAuthStore } from '../store/auth'
import { alertsApi } from '../api/endpoints'
import { getErrorMessage } from '../api/client'
import SeverityChip from '../components/SeverityChip'
import { SEVERITY_COLORS } from '../theme'
import type { Alert } from '../types'

type OutletCtx = { setSnackbar: (s: { open: boolean; message: string; severity: 'success' | 'error' }) => void }

type SeverityFilter = 'All' | 'Critical' | 'High' | 'Medium' | 'Low'

const SEVERITY_FILTER_MAP: Record<SeverityFilter, number | null> = {
  All: null, Critical: 4, High: 3, Medium: 2, Low: 1,
}

const FILTER_CHIPS: SeverityFilter[] = ['All', 'Critical', 'High', 'Medium', 'Low']

function AlertCard({ alert, onAck }: { alert: Alert; onAck: (id: string) => void }) {
  const borderColor =
    alert.severity >= 4 ? SEVERITY_COLORS[4]
    : alert.severity === 3 ? SEVERITY_COLORS[3]
    : alert.severity === 2 ? SEVERITY_COLORS[2]
    : SEVERITY_COLORS[1]

  return (
    <Paper
      sx={{
        p: 2.5,
        borderLeft: `4px solid ${borderColor}`,
        opacity: alert.isAcknowledged ? 0.55 : 1,
        transition: 'opacity 0.2s, box-shadow 0.2s',
        '&:hover': {
          boxShadow: alert.isAcknowledged ? undefined : `0 2px 16px ${alpha(borderColor, 0.2)}`,
        },
      }}
    >
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 2 }}>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 0.75, flexWrap: 'wrap' }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, fontSize: '0.9rem' }}>
              {alert.title}
            </Typography>
            <SeverityChip severity={alert.severity} />
            {alert.isAcknowledged && (
              <Chip
                icon={<SilencedIcon sx={{ fontSize: '12px !important' }} />}
                label="Acknowledged"
                size="small"
                sx={{ fontSize: '0.68rem', height: 20, color: '#8B949E', backgroundColor: alpha('#8B949E', 0.1) }}
              />
            )}
            {alert.isResolved && (
              <Chip
                icon={<AckIcon sx={{ fontSize: '12px !important' }} />}
                label="Resolved"
                size="small"
                sx={{ fontSize: '0.68rem', height: 20, color: '#3FB950', backgroundColor: alpha('#3FB950', 0.1) }}
              />
            )}
          </Box>
          <Typography variant="body2" sx={{ color: 'text.secondary', lineHeight: 1.6, mb: 1.5 }}>
            {alert.message}
          </Typography>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
              <Typography variant="caption" sx={{ color: 'text.secondary', fontSize: '0.72rem' }}>Source:</Typography>
              <Typography variant="caption" sx={{ color: '#58A6FF', fontFamily: 'monospace', fontSize: '0.72rem' }}>
                {alert.triggerSource}
              </Typography>
            </Box>
            <Typography variant="caption" sx={{ color: 'text.secondary', fontSize: '0.72rem' }}>
              {format(new Date(alert.createdAt), 'MMM d, yyyy · h:mm a')}
            </Typography>
          </Box>
        </Box>

        {!alert.isAcknowledged && (
          <Tooltip title="Acknowledge alert">
            <IconButton
              size="small"
              onClick={() => onAck(alert.id)}
              sx={{ color: 'text.secondary', flexShrink: 0, '&:hover': { color: '#3FB950', backgroundColor: alpha('#3FB950', 0.08) } }}
            >
              <AckIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
      </Box>
    </Paper>
  )
}

export default function Alerts() {
  const { setSnackbar } = useOutletContext<OutletCtx>()
  const user = useAuthStore((s) => s.user)
  const eid = user!.enterpriseId
  const qc = useQueryClient()

  const [severityFilter, setSeverityFilter] = useState<SeverityFilter>('All')
  const [acknowledging, setAcknowledging] = useState<Set<string>>(new Set())

  const alerts = useQuery({
    queryKey: ['alerts-active', eid],
    queryFn: () => alertsApi.getActive(eid),
    refetchInterval: 30_000,
  })

  useEffect(() => {
    if (alerts.error) setSnackbar({ open: true, message: getErrorMessage(alerts.error), severity: 'error' })
  }, [alerts.error])

  const ackMutation = useMutation({
    mutationFn: (alertId: string) => alertsApi.acknowledge(alertId),
    onMutate: (alertId: string) => setAcknowledging((s) => new Set([...s, alertId])),
    onSettled: (_data: unknown, _err: unknown, alertId: string) => {
      setAcknowledging((s) => { const ns = new Set(s); ns.delete(alertId); return ns })
    },
    onSuccess: (_data: unknown, alertId: string) => {
      setSnackbar({ open: true, message: 'Alert acknowledged.', severity: 'success' })
      qc.setQueryData(['alerts-active', eid], (prev: Alert[] | undefined) =>
        prev?.map((a) => (a.id === alertId ? { ...a, isAcknowledged: true } : a))
      )
    },
    onError: (e: unknown) => setSnackbar({ open: true, message: getErrorMessage(e), severity: 'error' }),
  })

  const allAlerts: Alert[] = alerts.data ?? []
  const sevValue = SEVERITY_FILTER_MAP[severityFilter]
  const filtered = sevValue !== null
    ? allAlerts.filter((a) => a.severity === sevValue)
    : allAlerts

  const unacknowledgedCount = allAlerts.filter((a) => !a.isAcknowledged).length

  const counts: Record<SeverityFilter, number> = {
    All: allAlerts.length,
    Critical: allAlerts.filter((a) => a.severity === 4).length,
    High: allAlerts.filter((a) => a.severity === 3).length,
    Medium: allAlerts.filter((a) => a.severity === 2).length,
    Low: allAlerts.filter((a) => a.severity === 1).length,
  }

  const chipColor: Record<SeverityFilter, string> = {
    All: '#2F81F7',
    Critical: SEVERITY_COLORS[4],
    High: SEVERITY_COLORS[3],
    Medium: SEVERITY_COLORS[2],
    Low: SEVERITY_COLORS[1],
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 3 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 700, letterSpacing: '-0.01em' }}>Alerts</Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary', mt: 0.5 }}>
            {alerts.isLoading
              ? 'Loading…'
              : `${unacknowledgedCount} unacknowledged alert${unacknowledgedCount !== 1 ? 's' : ''} · ${allAlerts.length} total`}
          </Typography>
        </Box>
        {unacknowledgedCount > 0 && (
          <Button
            variant="outlined"
            size="small"
            startIcon={<AckIcon />}
            onClick={() => allAlerts.filter((a) => !a.isAcknowledged).forEach((a) => ackMutation.mutate(a.id))}
            disabled={ackMutation.isPending}
          >
            Acknowledge All
          </Button>
        )}
      </Box>

      {/* Severity filter chips */}
      <Box sx={{ display: 'flex', gap: 1, mb: 2.5, flexWrap: 'wrap' }}>
        {FILTER_CHIPS.map((chip) => {
          const active = severityFilter === chip
          const color = chipColor[chip]
          return (
            <Chip
              key={chip}
              label={`${chip}${counts[chip] > 0 ? ` (${counts[chip]})` : ''}`}
              onClick={() => setSeverityFilter(chip)}
              sx={{
                cursor: 'pointer',
                fontWeight: active ? 600 : 400,
                color: active ? color : 'text.secondary',
                backgroundColor: active ? alpha(color, 0.15) : alpha('#ffffff', 0.04),
                border: `1px solid ${active ? alpha(color, 0.4) : '#30363D'}`,
                '&:hover': { backgroundColor: alpha(color, 0.1), color },
              }}
            />
          )
        })}
      </Box>

      {alerts.isLoading ? (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {[...Array(5)].map((_, i) => <Skeleton key={i} variant="rectangular" height={100} sx={{ borderRadius: 2 }} />)}
        </Box>
      ) : filtered.length === 0 ? (
        <Paper sx={{ p: 6, textAlign: 'center' }}>
          <NotificationsIcon sx={{ fontSize: 48, color: 'text.secondary', opacity: 0.4, mb: 1.5 }} />
          <Typography variant="h6" sx={{ fontWeight: 600, mb: 0.75 }}>No alerts</Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {severityFilter !== 'All'
              ? `No ${severityFilter.toLowerCase()} severity alerts found.`
              : 'No active alerts at this time. Your enterprise is looking healthy.'}
          </Typography>
        </Paper>
      ) : (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {filtered
            .slice()
            .sort((a, b) => {
              if (a.isAcknowledged !== b.isAcknowledged) return a.isAcknowledged ? 1 : -1
              return b.severity - a.severity
            })
            .map((alert) => (
              <AlertCard
                key={alert.id}
                alert={acknowledging.has(alert.id) ? { ...alert, isAcknowledged: true } : alert}
                onAck={(id) => ackMutation.mutate(id)}
              />
            ))}
        </Box>
      )}
    </Box>
  )
}

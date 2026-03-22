import { useEffect } from 'react'
import { useOutletContext } from 'react-router-dom'
import {
  Box,
  Grid,
  Paper,
  Typography,
  Skeleton,
  Chip,
  LinearProgress,
} from '@mui/material'
import {
  ErrorOutline as ErrorIcon,
  ShieldOutlined as ShieldIcon,
  NotificationsActive as AlertIcon,
  Description as DocIcon,
  Business as VendorIcon,
} from '@mui/icons-material'
import { alpha } from '@mui/material/styles'
import { useQuery } from '@tanstack/react-query'
import {
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  Tooltip as RechartsTooltip,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Legend,
} from 'recharts'
import { format } from 'date-fns'
import { useAuthStore } from '../store/auth'
import { complianceApi, alertsApi, documentsApi, vendorsApi, behavioralApi } from '../api/endpoints'
import { getErrorMessage } from '../api/client'
import StatCard from '../components/StatCard'
import SeverityChip from '../components/SeverityChip'
import RiskBadge from '../components/RiskBadge'
import { FRAMEWORK_NAMES, SEVERITY_NAMES } from '../types'

type OutletCtx = { setSnackbar: (s: { open: boolean; message: string; severity: 'success' | 'error' }) => void }

const SEVERITY_COLORS_CHART = ['#22C55E', '#EAB308', '#F97316', '#EF4444']

function scoreColor(score: number): string {
  if (score >= 80) return '#3FB950'
  if (score >= 60) return '#D29922'
  return '#F85149'
}

export default function Dashboard() {
  const { setSnackbar } = useOutletContext<OutletCtx>()
  const user = useAuthStore((s) => s.user)
  const eid = user!.enterpriseId

  const frameworks = useQuery({
    queryKey: ['frameworks', eid],
    queryFn: () => complianceApi.getFrameworks(eid),
  })

  const violations = useQuery({
    queryKey: ['violations-all', eid],
    queryFn: () => complianceApi.getViolations(eid, { pageSize: 100 }),
  })

  const alerts = useQuery({
    queryKey: ['alerts-active', eid],
    queryFn: () => alertsApi.getActive(eid),
    refetchInterval: 30_000,
  })

  const documents = useQuery({
    queryKey: ['documents', eid],
    queryFn: () => documentsApi.list(eid, { pageSize: 100 }),
  })

  const vendors = useQuery({
    queryKey: ['vendors', eid],
    queryFn: () => vendorsApi.list(eid),
  })

  const behavioral = useQuery({
    queryKey: ['behavioral', eid],
    queryFn: () => behavioralApi.getRecent(eid),
  })

  useEffect(() => {
    if (frameworks.error) setSnackbar({ open: true, message: getErrorMessage(frameworks.error), severity: 'error' })
  }, [frameworks.error])
  useEffect(() => {
    if (violations.error) setSnackbar({ open: true, message: getErrorMessage(violations.error), severity: 'error' })
  }, [violations.error])
  useEffect(() => {
    if (alerts.error) setSnackbar({ open: true, message: getErrorMessage(alerts.error), severity: 'error' })
  }, [alerts.error])
  useEffect(() => {
    if (behavioral.error) setSnackbar({ open: true, message: getErrorMessage(behavioral.error), severity: 'error' })
  }, [behavioral.error])

  const loading = frameworks.isLoading || violations.isLoading || alerts.isLoading

  const activeViolations = violations.data?.items.filter((v) => v.status === 1 || v.status === 2).length ?? 0
  const avgScore = frameworks.data?.length
    ? Math.round(frameworks.data.reduce((a, f) => a + f.complianceScore, 0) / frameworks.data.length)
    : 0
  const activeAlerts = alerts.data?.filter((a) => !a.isAcknowledged).length ?? 0
  const docsAnalyzed = documents.data?.totalCount ?? 0
  const vendorsAssessed = vendors.data?.totalCount ?? 0

  const frameworkChartData = (frameworks.data ?? []).map((f) => ({
    name: FRAMEWORK_NAMES[f.framework] ?? `Framework ${f.framework}`,
    score: Math.round(f.complianceScore),
    fill: scoreColor(f.complianceScore),
  }))

  const severityBuckets = [1, 2, 3, 4].map((sev) => ({
    name: SEVERITY_NAMES[sev],
    value: violations.data?.items.filter((v) => v.severity === sev).length ?? 0,
    color: SEVERITY_COLORS_CHART[sev - 1],
  })).filter((b) => b.value > 0)

  const recentAlerts = (alerts.data ?? []).slice(0, 6)
  const recentAnomalies = (behavioral.data?.items ?? []).slice(0, 5)

  return (
    <Box>
      <Box sx={{ mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700, letterSpacing: '-0.01em' }}>
          Dashboard
        </Typography>
        <Typography variant="body2" sx={{ color: 'text.secondary', mt: 0.5 }}>
          Enterprise compliance and risk overview
        </Typography>
      </Box>

      {/* KPI Row */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        {[
          { title: 'Active Violations', value: loading ? '–' : activeViolations, icon: ErrorIcon, color: '#F85149', subtitle: 'open or in remediation' },
          { title: 'Avg Compliance Score', value: loading ? '–' : `${avgScore}%`, icon: ShieldIcon, color: scoreColor(avgScore), subtitle: 'across all frameworks' },
          { title: 'Active Alerts', value: loading ? '–' : activeAlerts, icon: AlertIcon, color: '#D29922', subtitle: 'unacknowledged' },
          { title: 'Documents Analyzed', value: loading ? '–' : docsAnalyzed, icon: DocIcon, color: '#2F81F7', subtitle: 'total documents' },
          { title: 'Vendors Assessed', value: loading ? '–' : vendorsAssessed, icon: VendorIcon, color: '#A371F7', subtitle: 'active vendors' },
        ].map((card) => (
          <Grid key={card.title} item xs={12} sm={6} md={4} lg={12 / 5}>
            <StatCard {...card} loading={loading} />
          </Grid>
        ))}
      </Grid>

      {/* Charts row */}
      <Grid container spacing={2.5} sx={{ mb: 3 }}>
        <Grid item xs={12} md={7}>
          <Paper sx={{ p: 2.5, height: 320 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 2 }}>
              Compliance Score by Framework
            </Typography>
            {frameworks.isLoading ? (
              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5, pt: 1 }}>
                {[...Array(5)].map((_, i) => (
                  <Box key={i} sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
                    <Skeleton width={70} height={16} />
                    <Skeleton variant="rectangular" height={20} sx={{ flex: 1, borderRadius: 1 }} />
                    <Skeleton width={36} height={16} />
                  </Box>
                ))}
              </Box>
            ) : frameworkChartData.length === 0 ? (
              <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: 220 }}>
                <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                  No framework data available
                </Typography>
              </Box>
            ) : (
              <ResponsiveContainer width="100%" height={240}>
                <BarChart data={frameworkChartData} layout="vertical" margin={{ top: 0, right: 32, left: 16, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#21262D" horizontal={false} />
                  <XAxis
                    type="number"
                    domain={[0, 100]}
                    tick={{ fill: '#8B949E', fontSize: 11 }}
                    axisLine={{ stroke: '#30363D' }}
                    tickLine={false}
                    tickFormatter={(v) => `${v}%`}
                  />
                  <YAxis
                    dataKey="name"
                    type="category"
                    tick={{ fill: '#8B949E', fontSize: 12 }}
                    axisLine={false}
                    tickLine={false}
                    width={72}
                  />
                  <RechartsTooltip
                    formatter={(value: number) => [`${value}%`, 'Score']}
                    contentStyle={{ backgroundColor: '#21262D', border: '1px solid #30363D', borderRadius: 8, fontSize: 13 }}
                    labelStyle={{ color: '#E6EDF3' }}
                    itemStyle={{ color: '#8B949E' }}
                  />
                  <Bar dataKey="score" radius={[0, 4, 4, 0]} maxBarSize={28}>
                    {frameworkChartData.map((entry, index) => (
                      <Cell key={index} fill={entry.fill} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            )}
          </Paper>
        </Grid>

        <Grid item xs={12} md={5}>
          <Paper sx={{ p: 2.5, height: 320 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 2 }}>
              Violations by Severity
            </Typography>
            {violations.isLoading ? (
              <Box sx={{ display: 'flex', justifyContent: 'center', pt: 4 }}>
                <Skeleton variant="circular" width={180} height={180} />
              </Box>
            ) : severityBuckets.length === 0 ? (
              <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: 220 }}>
                <Typography variant="body2" sx={{ color: 'text.secondary' }}>No violations found</Typography>
              </Box>
            ) : (
              <ResponsiveContainer width="100%" height={240}>
                <PieChart>
                  <Pie data={severityBuckets} cx="50%" cy="45%" innerRadius={60} outerRadius={90} paddingAngle={3} dataKey="value">
                    {severityBuckets.map((entry, i) => <Cell key={i} fill={entry.color} />)}
                  </Pie>
                  <RechartsTooltip
                    contentStyle={{ backgroundColor: '#21262D', border: '1px solid #30363D', borderRadius: 8, fontSize: 13 }}
                    formatter={(value: number, name: string) => [value, name]}
                    labelStyle={{ color: '#E6EDF3' }}
                    itemStyle={{ color: '#8B949E' }}
                  />
                  <Legend iconType="circle" iconSize={8} formatter={(value) => <span style={{ color: '#8B949E', fontSize: 12 }}>{value}</span>} />
                </PieChart>
              </ResponsiveContainer>
            )}
          </Paper>
        </Grid>
      </Grid>

      {/* Bottom row */}
      <Grid container spacing={2.5}>
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 2.5 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 2 }}>Recent Alerts</Typography>
            {alerts.isLoading ? (
              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
                {[...Array(4)].map((_, i) => <Skeleton key={i} variant="rectangular" height={44} sx={{ borderRadius: 1 }} />)}
              </Box>
            ) : recentAlerts.length === 0 ? (
              <Box sx={{ py: 4, textAlign: 'center' }}>
                <Typography variant="body2" sx={{ color: 'text.secondary' }}>No active alerts</Typography>
              </Box>
            ) : (
              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.75 }}>
                {recentAlerts.map((alert) => (
                  <Box
                    key={alert.id}
                    sx={{
                      display: 'flex', alignItems: 'center', gap: 2, p: 1.5, borderRadius: 1.5,
                      backgroundColor: alpha('#ffffff', 0.02), border: '1px solid #21262D',
                      borderLeft: `3px solid ${alert.severity >= 4 ? '#EF4444' : alert.severity === 3 ? '#F97316' : alert.severity === 2 ? '#EAB308' : '#22C55E'}`,
                    }}
                  >
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                      <Typography variant="body2" sx={{ fontWeight: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', opacity: alert.isAcknowledged ? 0.5 : 1 }}>
                        {alert.title}
                      </Typography>
                      <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                        {format(new Date(alert.createdAt), 'MMM d, h:mm a')}
                      </Typography>
                    </Box>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexShrink: 0 }}>
                      <SeverityChip severity={alert.severity} />
                      {alert.isAcknowledged && (
                        <Chip label="Ack'd" size="small" sx={{ fontSize: '0.65rem', height: 18, opacity: 0.6 }} />
                      )}
                    </Box>
                  </Box>
                ))}
              </Box>
            )}
          </Paper>
        </Grid>

        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 2.5 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 2 }}>Behavioral Anomalies</Typography>
            {behavioral.isLoading ? (
              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
                {[...Array(4)].map((_, i) => <Skeleton key={i} variant="rectangular" height={44} sx={{ borderRadius: 1 }} />)}
              </Box>
            ) : recentAnomalies.length === 0 ? (
              <Box sx={{ py: 4, textAlign: 'center' }}>
                <Typography variant="body2" sx={{ color: 'text.secondary' }}>No recent anomalies detected</Typography>
              </Box>
            ) : (
              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.75 }}>
                {recentAnomalies.map((event) => (
                  <Box
                    key={event.id}
                    sx={{
                      display: 'flex', alignItems: 'center', gap: 2, p: 1.5, borderRadius: 1.5,
                      backgroundColor: alpha('#ffffff', 0.02), border: '1px solid #21262D',
                    }}
                  >
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                      <Typography variant="body2" sx={{ fontWeight: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {event.description}
                      </Typography>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mt: 0.25 }}>
                        <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                          {event.entityType} · {format(new Date(event.occurredAt), 'MMM d, h:mm a')}
                        </Typography>
                        {event.isInvestigated && (
                          <Chip label="Investigated" size="small" sx={{ fontSize: '0.6rem', height: 16, color: '#3FB950', backgroundColor: alpha('#3FB950', 0.1) }} />
                        )}
                      </Box>
                    </Box>
                    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 0.5, flexShrink: 0 }}>
                      <RiskBadge riskLevel={event.riskLevel} />
                      <Typography variant="caption" sx={{ color: 'text.secondary', fontSize: '0.68rem' }}>
                        Score: {event.anomalyScore.toFixed(2)}
                      </Typography>
                    </Box>
                  </Box>
                ))}
              </Box>
            )}
          </Paper>
        </Grid>
      </Grid>
    </Box>
  )
}

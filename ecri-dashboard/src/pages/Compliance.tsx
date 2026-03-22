import { useState, useEffect } from 'react'
import { useOutletContext } from 'react-router-dom'
import {
  Box,
  Paper,
  Typography,
  Tabs,
  Tab,
  Grid,
  LinearProgress,
  Button,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  CircularProgress,
  Skeleton,
  Chip,
  Tooltip,
} from '@mui/material'
import {
  Shield as ShieldIcon,
  PlayArrow as RunIcon,
  CheckCircle as ResolveIcon,
  ErrorOutline as ViolationIcon,
} from '@mui/icons-material'
import { alpha } from '@mui/material/styles'
import { DataGrid, GridColDef, GridRenderCellParams } from '@mui/x-data-grid'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { format } from 'date-fns'
import { useAuthStore } from '../store/auth'
import { complianceApi } from '../api/endpoints'
import { getErrorMessage } from '../api/client'
import SeverityChip from '../components/SeverityChip'
import { FRAMEWORK_NAMES, STATUS_NAMES, FRAMEWORK_ENUM, type ViolationSummaryDto } from '../types'

type OutletCtx = { setSnackbar: (s: { open: boolean; message: string; severity: 'success' | 'error' }) => void }

const FRAMEWORKS = Object.entries(FRAMEWORK_ENUM)

function scoreColor(score: number): string {
  if (score >= 80) return '#3FB950'
  if (score >= 60) return '#D29922'
  return '#F85149'
}

function statusChip(status: number) {
  const name = STATUS_NAMES[status] ?? String(status)
  const colors: Record<number, string> = {
    1: '#F85149',
    2: '#D29922',
    3: '#3FB950',
    4: '#8B949E',
    5: '#58A6FF',
  }
  const color = colors[status] ?? '#8B949E'
  return (
    <Chip
      label={name}
      size="small"
      sx={{
        color,
        backgroundColor: alpha(color, 0.12),
        border: `1px solid ${alpha(color, 0.3)}`,
        fontSize: '0.7rem',
        fontWeight: 500,
      }}
    />
  )
}

export default function Compliance() {
  const { setSnackbar } = useOutletContext<OutletCtx>()
  const user = useAuthStore((s) => s.user)
  const eid = user!.enterpriseId
  const qc = useQueryClient()

  const [activeFramework, setActiveFramework] = useState(Object.values(FRAMEWORK_ENUM)[0])
  const [statusFilter, setStatusFilter] = useState<number | ''>('')
  const [checkDialogOpen, setCheckDialogOpen] = useState(false)
  const [checkScope, setCheckScope] = useState('')
  const [resolveDialog, setResolveDialog] = useState<{ open: boolean; violation: ViolationSummaryDto | null }>({
    open: false,
    violation: null,
  })
  const [resolveText, setResolveText] = useState('')
  const [resolveError, setResolveError] = useState('')

  const frameworks = useQuery({
    queryKey: ['frameworks', eid],
    queryFn: () => complianceApi.getFrameworks(eid),
  })

  const violations = useQuery({
    queryKey: ['violations', eid, activeFramework, statusFilter],
    queryFn: () =>
      complianceApi.getViolations(eid, {
        framework: activeFramework,
        status: statusFilter === '' ? undefined : (statusFilter as number),
        pageSize: 200,
      }),
  })

  useEffect(() => {
    if (frameworks.error) setSnackbar({ open: true, message: getErrorMessage(frameworks.error), severity: 'error' })
  }, [frameworks.error])
  useEffect(() => {
    if (violations.error) setSnackbar({ open: true, message: getErrorMessage(violations.error), severity: 'error' })
  }, [violations.error])

  const runCheck = useMutation({
    mutationFn: () =>
      complianceApi.runCheck({ enterpriseId: eid, framework: activeFramework, scope: checkScope || undefined }),
    onSuccess: () => {
      setSnackbar({ open: true, message: 'Compliance check initiated successfully.', severity: 'success' })
      setCheckDialogOpen(false)
      setCheckScope('')
      qc.invalidateQueries({ queryKey: ['violations'] })
      qc.invalidateQueries({ queryKey: ['frameworks'] })
    },
    onError: (e: unknown) => setSnackbar({ open: true, message: getErrorMessage(e), severity: 'error' }),
  })

  const resolveViolation = useMutation({
    mutationFn: (violationId: string) =>
      complianceApi.resolveViolation(violationId, { resolution: resolveText, resolvedBy: user!.email }),
    onSuccess: () => {
      setSnackbar({ open: true, message: 'Violation resolved successfully.', severity: 'success' })
      setResolveDialog({ open: false, violation: null })
      setResolveText('')
      setResolveError('')
      qc.invalidateQueries({ queryKey: ['violations'] })
    },
    onError: (e: unknown) => setSnackbar({ open: true, message: getErrorMessage(e), severity: 'error' }),
  })

  const currentFramework = frameworks.data?.find((f) => f.framework === activeFramework)

  const columns: GridColDef[] = [
    {
      field: 'ruleCode',
      headerName: 'Rule Code',
      width: 130,
      renderCell: (params: GridRenderCellParams) => (
        <Typography variant="caption" sx={{ fontFamily: 'monospace', color: '#58A6FF', fontWeight: 500 }}>
          {params.value as string}
        </Typography>
      ),
    },
    { field: 'title', headerName: 'Title', flex: 1, minWidth: 200 },
    {
      field: 'severity',
      headerName: 'Severity',
      width: 110,
      renderCell: (params: GridRenderCellParams) => <SeverityChip severity={params.value as number} />,
    },
    {
      field: 'status',
      headerName: 'Status',
      width: 140,
      renderCell: (params: GridRenderCellParams) => statusChip(params.value as number),
    },
    {
      field: 'affectedResource',
      headerName: 'Affected Resource',
      flex: 1,
      minWidth: 180,
      renderCell: (params: GridRenderCellParams) => (
        <Tooltip title={params.value as string}>
          <Typography variant="body2" sx={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', color: 'text.secondary', fontSize: '0.8rem' }}>
            {params.value as string}
          </Typography>
        </Tooltip>
      ),
    },
    {
      field: 'detectedAt',
      headerName: 'Detected',
      width: 160,
      renderCell: (params: GridRenderCellParams) => (
        <Typography variant="body2" sx={{ color: 'text.secondary', fontSize: '0.8rem' }}>
          {format(new Date(params.value as string), 'MMM d, yyyy')}
        </Typography>
      ),
    },
    {
      field: 'actions',
      headerName: 'Actions',
      width: 100,
      sortable: false,
      filterable: false,
      renderCell: (params: GridRenderCellParams) => {
        const row = params.row as ViolationSummaryDto
        const canResolve = row.status === 1 || row.status === 2
        return (
          <Button
            size="small"
            variant="outlined"
            disabled={!canResolve}
            startIcon={<ResolveIcon sx={{ fontSize: '14px !important' }} />}
            onClick={() => {
              setResolveDialog({ open: true, violation: row })
              setResolveText('')
              setResolveError('')
            }}
            sx={{ fontSize: '0.72rem', py: 0.25, px: 1 }}
          >
            Resolve
          </Button>
        )
      },
    },
  ]

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 3 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 700, letterSpacing: '-0.01em' }}>Compliance</Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary', mt: 0.5 }}>
            Regulatory framework monitoring and violation management
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<RunIcon />} onClick={() => setCheckDialogOpen(true)}>
          Run Compliance Check
        </Button>
      </Box>

      <Paper sx={{ mb: 2.5 }}>
        <Tabs value={activeFramework} onChange={(_, v: number) => setActiveFramework(v)} variant="scrollable" scrollButtons="auto">
          {FRAMEWORKS.map(([name, value]) => (
            <Tab key={value} label={name} value={Number(value)} />
          ))}
        </Tabs>

        <Box sx={{ p: 2.5 }}>
          {frameworks.isLoading ? (
            <Grid container spacing={3}>
              {[...Array(4)].map((_, i) => <Grid key={i} item xs={6} md={3}><Skeleton height={60} /></Grid>)}
            </Grid>
          ) : !currentFramework ? (
            <Box sx={{ py: 2, display: 'flex', alignItems: 'center', gap: 1 }}>
              <ShieldIcon sx={{ color: 'text.secondary', fontSize: 20 }} />
              <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                No data available for this framework. Run a compliance check to get started.
              </Typography>
            </Box>
          ) : (
            <Grid container spacing={3} alignItems="center">
              <Grid item xs={12} sm={6} md={3}>
                <Typography variant="caption" sx={{ color: 'text.secondary', textTransform: 'uppercase', fontSize: '0.68rem', letterSpacing: '0.06em' }}>
                  Compliance Score
                </Typography>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mt: 0.75 }}>
                  <Typography variant="h4" sx={{ fontWeight: 700, color: scoreColor(currentFramework.complianceScore) }}>
                    {Math.round(currentFramework.complianceScore)}%
                  </Typography>
                </Box>
                <LinearProgress
                  variant="determinate"
                  value={currentFramework.complianceScore}
                  sx={{ mt: 1, height: 6, borderRadius: 3, '& .MuiLinearProgress-bar': { backgroundColor: scoreColor(currentFramework.complianceScore) } }}
                />
              </Grid>
              {[
                { label: 'Total Rules', value: currentFramework.totalRules },
                { label: 'Open Violations', value: currentFramework.violationsOpen },
                { label: 'Resolved', value: currentFramework.violationsResolved },
              ].map(({ label, value }) => (
                <Grid key={label} item xs={6} sm={3} md={2}>
                  <Typography variant="caption" sx={{ color: 'text.secondary', textTransform: 'uppercase', fontSize: '0.68rem', letterSpacing: '0.06em' }}>
                    {label}
                  </Typography>
                  <Typography variant="h5" sx={{ fontWeight: 700, mt: 0.5 }}>{value}</Typography>
                </Grid>
              ))}
              <Grid item xs={12} sm={6} md={3}>
                <Typography variant="caption" sx={{ color: 'text.secondary', textTransform: 'uppercase', fontSize: '0.68rem', letterSpacing: '0.06em' }}>
                  Last Checked
                </Typography>
                <Typography variant="body2" sx={{ mt: 0.5, fontWeight: 500 }}>
                  {format(new Date(currentFramework.lastChecked), 'MMM d, yyyy h:mm a')}
                </Typography>
              </Grid>
            </Grid>
          )}
        </Box>
      </Paper>

      <Paper sx={{ p: 0 }}>
        <Box sx={{ px: 2.5, py: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid #30363D' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
            <ViolationIcon sx={{ color: 'text.secondary', fontSize: 18 }} />
            <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>Violations</Typography>
            {!violations.isLoading && (
              <Chip label={violations.data?.totalCount ?? 0} size="small" sx={{ height: 20, fontSize: '0.7rem' }} />
            )}
          </Box>
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel>Filter by Status</InputLabel>
            <Select
              value={statusFilter}
              label="Filter by Status"
              onChange={(e) => setStatusFilter(e.target.value as number | '')}
            >
              <MenuItem value="">All Statuses</MenuItem>
              {Object.entries(STATUS_NAMES).map(([k, v]) => (
                <MenuItem key={k} value={Number(k)}>{v}</MenuItem>
              ))}
            </Select>
          </FormControl>
        </Box>

        {violations.isLoading ? (
          <Box sx={{ p: 2.5, display: 'flex', flexDirection: 'column', gap: 1 }}>
            {[...Array(5)].map((_, i) => <Skeleton key={i} variant="rectangular" height={52} sx={{ borderRadius: 1 }} />)}
          </Box>
        ) : (
          <DataGrid
            rows={violations.data?.items ?? []}
            columns={columns}
            getRowId={(row) => (row as ViolationSummaryDto).violationId}
            autoHeight
            disableRowSelectionOnClick
            pageSizeOptions={[25, 50, 100]}
            initialState={{ pagination: { paginationModel: { pageSize: 25 } } }}
            sx={{ border: 'none' }}
            slots={{
              noRowsOverlay: () => (
                <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '100%', gap: 1, py: 6 }}>
                  <ShieldIcon sx={{ fontSize: 40, color: '#3FB950', opacity: 0.5 }} />
                  <Typography variant="body1" sx={{ fontWeight: 500 }}>No violations found</Typography>
                  <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                    This framework is fully compliant or no check has been run yet.
                  </Typography>
                </Box>
              ),
            }}
          />
        )}
      </Paper>

      {/* Run Check Dialog */}
      <Dialog open={checkDialogOpen} onClose={() => setCheckDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ fontWeight: 600 }}>Run Compliance Check</DialogTitle>
        <DialogContent sx={{ pt: '16px !important' }}>
          <Typography variant="body2" sx={{ color: 'text.secondary', mb: 2 }}>
            Run a compliance check for{' '}
            <strong style={{ color: '#E6EDF3' }}>{FRAMEWORK_NAMES[activeFramework]}</strong>. This will scan your enterprise resources and report any violations.
          </Typography>
          <TextField
            label="Scope (optional)"
            placeholder="e.g. production-cluster, data-services"
            value={checkScope}
            onChange={(e) => setCheckScope(e.target.value)}
            fullWidth
            helperText="Leave blank to scan all resources"
          />
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2.5 }}>
          <Button onClick={() => setCheckDialogOpen(false)} disabled={runCheck.isPending}>Cancel</Button>
          <Button
            variant="contained"
            onClick={() => runCheck.mutate()}
            disabled={runCheck.isPending}
            startIcon={runCheck.isPending ? <CircularProgress size={14} color="inherit" /> : <RunIcon />}
          >
            {runCheck.isPending ? 'Running…' : 'Run Check'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Resolve Dialog */}
      <Dialog open={resolveDialog.open} onClose={() => setResolveDialog({ open: false, violation: null })} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ fontWeight: 600 }}>Resolve Violation</DialogTitle>
        <DialogContent sx={{ pt: '16px !important' }}>
          {resolveDialog.violation && (
            <Box sx={{ mb: 2, p: 1.5, borderRadius: 1.5, backgroundColor: alpha('#2F81F7', 0.05), border: '1px solid #30363D' }}>
              <Typography variant="caption" sx={{ color: 'text.secondary', textTransform: 'uppercase', fontSize: '0.68rem' }}>
                Violation
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 500, mt: 0.25 }}>
                [{resolveDialog.violation.ruleCode}] {resolveDialog.violation.title}
              </Typography>
            </Box>
          )}
          <TextField
            label="Resolution notes"
            placeholder="Describe the remediation steps taken…"
            value={resolveText}
            onChange={(e) => { setResolveText(e.target.value); setResolveError('') }}
            error={!!resolveError}
            helperText={resolveError}
            fullWidth
            multiline
            rows={4}
            required
          />
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2.5 }}>
          <Button onClick={() => setResolveDialog({ open: false, violation: null })} disabled={resolveViolation.isPending}>
            Cancel
          </Button>
          <Button
            variant="contained"
            color="success"
            onClick={() => {
              if (!resolveText.trim()) { setResolveError('Resolution notes are required.'); return }
              if (resolveDialog.violation) resolveViolation.mutate(resolveDialog.violation.violationId)
            }}
            disabled={resolveViolation.isPending}
            startIcon={resolveViolation.isPending ? <CircularProgress size={14} color="inherit" /> : <ResolveIcon />}
          >
            {resolveViolation.isPending ? 'Resolving…' : 'Resolve'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}

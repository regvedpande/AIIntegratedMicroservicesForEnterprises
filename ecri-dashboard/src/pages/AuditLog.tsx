import { useState, useEffect } from 'react'
import { useOutletContext } from 'react-router-dom'
import {
  Box,
  Paper,
  Typography,
  Button,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  TextField,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  CircularProgress,
  Chip,
  Grid,
  Skeleton,
} from '@mui/material'
import {
  History as HistoryIcon,
  GetApp as ExportIcon,
  Search as SearchIcon,
} from '@mui/icons-material'
import { alpha } from '@mui/material/styles'
import { DataGrid, GridColDef, GridRenderCellParams } from '@mui/x-data-grid'
import { useQuery, useMutation } from '@tanstack/react-query'
import { format, subDays } from 'date-fns'
import { useAuthStore } from '../store/auth'
import { auditApi } from '../api/endpoints'
import { getErrorMessage } from '../api/client'
import { AUDIT_ACTION_NAMES, type AuditEntrySummary } from '../types'

type OutletCtx = { setSnackbar: (s: { open: boolean; message: string; severity: 'success' | 'error' }) => void }

const ACTION_COLOR: Record<number, string> = {
  0: '#3FB950', 1: '#58A6FF', 2: '#F85149', 3: '#8B949E', 4: '#D29922',
  5: '#A371F7', 6: '#F85149', 7: '#2F81F7', 8: '#3FB950', 9: '#D29922',
  10: '#58A6FF', 11: '#8B949E', 12: '#F97316',
}

export default function AuditLog() {
  const { setSnackbar } = useOutletContext<OutletCtx>()
  const user = useAuthStore((s) => s.user)
  const eid = user!.enterpriseId

  const [fromDate, setFromDate] = useState(format(subDays(new Date(), 30), 'yyyy-MM-dd'))
  const [toDate, setToDate] = useState(format(new Date(), 'yyyy-MM-dd'))
  const [actionFilter, setActionFilter] = useState<number | ''>('')
  const [emailFilter, setEmailFilter] = useState('')
  const [reportDialogOpen, setReportDialogOpen] = useState(false)
  const [reportFormat, setReportFormat] = useState<'PDF' | 'CSV' | 'JSON'>('PDF')
  const [page, setPage] = useState(0)
  const [pageSize, setPageSize] = useState(50)

  const auditQuery = useQuery({
    queryKey: ['audit', eid, fromDate, toDate, actionFilter, emailFilter, page, pageSize],
    queryFn: () =>
      auditApi.query({
        enterpriseId: eid,
        fromDate: fromDate ? new Date(fromDate).toISOString() : undefined,
        toDate: toDate ? new Date(toDate + 'T23:59:59').toISOString() : undefined,
        action: actionFilter === '' ? undefined : (actionFilter as number),
        userEmail: emailFilter.trim() || undefined,
        page: page + 1,
        pageSize,
      }),
    placeholderData: (prev) => prev,
  })

  useEffect(() => {
    if (auditQuery.error) setSnackbar({ open: true, message: getErrorMessage(auditQuery.error), severity: 'error' })
  }, [auditQuery.error])

  const generateReport = useMutation({
    mutationFn: () =>
      auditApi.generateReport({
        enterpriseId: eid,
        fromDate: new Date(fromDate).toISOString(),
        toDate: new Date(toDate + 'T23:59:59').toISOString(),
        format: reportFormat,
      }),
    onSuccess: () => {
      setSnackbar({ open: true, message: 'Audit report generation started.', severity: 'success' })
      setReportDialogOpen(false)
    },
    onError: (e: unknown) => setSnackbar({ open: true, message: getErrorMessage(e), severity: 'error' }),
  })

  const columns: GridColDef[] = [
    {
      field: 'occurredAt',
      headerName: 'Timestamp',
      width: 170,
      renderCell: (params: GridRenderCellParams) => (
        <Typography variant="body2" sx={{ fontSize: '0.8rem', color: 'text.secondary', fontFamily: 'monospace' }}>
          {format(new Date(params.value as string), 'MMM d, HH:mm:ss')}
        </Typography>
      ),
    },
    {
      field: 'userEmail',
      headerName: 'User',
      width: 220,
      renderCell: (params: GridRenderCellParams) => (
        <Typography variant="body2" sx={{ fontSize: '0.82rem', fontWeight: 500 }}>{params.value as string}</Typography>
      ),
    },
    {
      field: 'action',
      headerName: 'Action',
      width: 200,
      renderCell: (params: GridRenderCellParams) => {
        const actionNum = params.value as number
        const name = AUDIT_ACTION_NAMES[actionNum] ?? String(actionNum)
        const color = ACTION_COLOR[actionNum] ?? '#8B949E'
        return (
          <Chip
            label={name}
            size="small"
            sx={{
              color,
              backgroundColor: alpha(color, 0.1),
              border: `1px solid ${alpha(color, 0.25)}`,
              fontSize: '0.72rem',
              fontWeight: 500,
            }}
          />
        )
      },
    },
    {
      field: 'resourceType',
      headerName: 'Resource Type',
      width: 150,
      renderCell: (params: GridRenderCellParams) => (
        <Typography variant="caption" sx={{ color: '#58A6FF', fontFamily: 'monospace' }}>{params.value as string}</Typography>
      ),
    },
    {
      field: 'resourceName',
      headerName: 'Resource',
      flex: 1,
      minWidth: 180,
      renderCell: (params: GridRenderCellParams) => (
        <Typography variant="body2" sx={{ fontSize: '0.82rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {params.value as string}
        </Typography>
      ),
    },
    {
      field: 'description',
      headerName: 'Description',
      flex: 1,
      minWidth: 200,
      renderCell: (params: GridRenderCellParams) => (
        <Typography
          variant="body2"
          sx={{ fontSize: '0.8rem', color: 'text.secondary', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
          title={params.value as string}
        >
          {params.value as string}
        </Typography>
      ),
    },
    {
      field: 'ipAddress',
      headerName: 'IP Address',
      width: 130,
      renderCell: (params: GridRenderCellParams) => (
        <Typography variant="caption" sx={{ fontFamily: 'monospace', color: 'text.secondary' }}>{params.value as string}</Typography>
      ),
    },
  ]

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 3 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 700, letterSpacing: '-0.01em' }}>Audit Log</Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary', mt: 0.5 }}>
            Complete audit trail of all enterprise actions and events
          </Typography>
        </Box>
        <Button variant="outlined" startIcon={<ExportIcon />} onClick={() => setReportDialogOpen(true)}>
          Generate Report
        </Button>
      </Box>

      <Paper sx={{ p: 2, mb: 2.5 }}>
        <Grid container spacing={2} alignItems="center">
          <Grid item xs={12} sm={6} md={3}>
            <TextField
              label="From Date" type="date" value={fromDate}
              onChange={(e) => { setFromDate(e.target.value); setPage(0) }}
              fullWidth InputLabelProps={{ shrink: true }}
            />
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <TextField
              label="To Date" type="date" value={toDate}
              onChange={(e) => { setToDate(e.target.value); setPage(0) }}
              fullWidth InputLabelProps={{ shrink: true }}
            />
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <FormControl fullWidth size="small">
              <InputLabel>Action Type</InputLabel>
              <Select value={actionFilter} label="Action Type" onChange={(e) => { setActionFilter(e.target.value as number | ''); setPage(0) }}>
                <MenuItem value="">All Actions</MenuItem>
                {Object.entries(AUDIT_ACTION_NAMES).map(([k, v]) => (
                  <MenuItem key={k} value={Number(k)}>{v}</MenuItem>
                ))}
              </Select>
            </FormControl>
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <TextField
              label="User Email" value={emailFilter}
              onChange={(e) => { setEmailFilter(e.target.value); setPage(0) }}
              fullWidth placeholder="Search by email…"
              InputProps={{ startAdornment: <SearchIcon sx={{ fontSize: 16, color: 'text.secondary', mr: 0.5 }} /> }}
            />
          </Grid>
        </Grid>
      </Paper>

      <Paper sx={{ p: 0 }}>
        <Box sx={{ px: 2.5, py: 2, borderBottom: '1px solid #30363D', display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <HistoryIcon sx={{ color: 'text.secondary', fontSize: 18 }} />
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>Audit Entries</Typography>
          {!auditQuery.isLoading && (
            <Chip label={auditQuery.data?.totalCount ?? 0} size="small" sx={{ height: 20, fontSize: '0.7rem' }} />
          )}
        </Box>

        {auditQuery.isLoading ? (
          <Box sx={{ p: 2.5, display: 'flex', flexDirection: 'column', gap: 1 }}>
            {[...Array(8)].map((_, i) => <Skeleton key={i} variant="rectangular" height={44} sx={{ borderRadius: 1 }} />)}
          </Box>
        ) : (
          <DataGrid
            rows={auditQuery.data?.items ?? []}
            columns={columns}
            getRowId={(row) => (row as AuditEntrySummary).id}
            autoHeight
            disableRowSelectionOnClick
            density="compact"
            rowCount={auditQuery.data?.totalCount ?? 0}
            paginationMode="server"
            paginationModel={{ page, pageSize }}
            onPaginationModelChange={(model) => {
              setPage(model.page)
              setPageSize(model.pageSize)
            }}
            pageSizeOptions={[25, 50, 100]}
            sx={{ border: 'none' }}
            slots={{
              noRowsOverlay: () => (
                <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '100%', gap: 1, py: 6 }}>
                  <HistoryIcon sx={{ fontSize: 40, color: 'text.secondary', opacity: 0.4 }} />
                  <Typography variant="body1" sx={{ fontWeight: 500 }}>No audit entries found</Typography>
                  <Typography variant="body2" sx={{ color: 'text.secondary' }}>Try adjusting the date range or filters.</Typography>
                </Box>
              ),
            }}
          />
        )}
      </Paper>

      <Dialog open={reportDialogOpen} onClose={() => setReportDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ fontWeight: 600 }}>Generate Audit Report</DialogTitle>
        <DialogContent sx={{ pt: '16px !important', display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            Generate a comprehensive audit report for the selected date range.
          </Typography>
          <Box sx={{ display: 'flex', gap: 2 }}>
            <TextField label="From Date" type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} fullWidth InputLabelProps={{ shrink: true }} />
            <TextField label="To Date" type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} fullWidth InputLabelProps={{ shrink: true }} />
          </Box>
          <FormControl fullWidth size="small">
            <InputLabel>Format</InputLabel>
            <Select value={reportFormat} label="Format" onChange={(e) => setReportFormat(e.target.value as 'PDF' | 'CSV' | 'JSON')}>
              <MenuItem value="PDF">PDF</MenuItem>
              <MenuItem value="CSV">CSV</MenuItem>
              <MenuItem value="JSON">JSON</MenuItem>
            </Select>
          </FormControl>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2.5 }}>
          <Button onClick={() => setReportDialogOpen(false)} disabled={generateReport.isPending}>Cancel</Button>
          <Button
            variant="contained"
            onClick={() => generateReport.mutate()}
            disabled={generateReport.isPending || !fromDate || !toDate}
            startIcon={generateReport.isPending ? <CircularProgress size={14} color="inherit" /> : <ExportIcon />}
          >
            {generateReport.isPending ? 'Generating…' : 'Generate'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}

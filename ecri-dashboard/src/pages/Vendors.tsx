import { useState, useEffect } from 'react'
import { useOutletContext } from 'react-router-dom'
import {
  Box,
  Paper,
  Grid,
  Typography,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  CircularProgress,
  Skeleton,
  Chip,
  Divider,
  LinearProgress,
} from '@mui/material'
import {
  Business as BusinessIcon,
  Add as AddIcon,
  Verified as VerifiedIcon,
} from '@mui/icons-material'
import { alpha } from '@mui/material/styles'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { format } from 'date-fns'
import { useAuthStore } from '../store/auth'
import { vendorsApi, behavioralApi } from '../api/endpoints'
import { getErrorMessage } from '../api/client'
import RiskBadge from '../components/RiskBadge'
import { RISK_LEVEL_NAMES, type VendorRiskSummary, type BehavioralRiskEvent } from '../types'
import { RISK_LEVEL_COLORS } from '../theme'

type OutletCtx = { setSnackbar: (s: { open: boolean; message: string; severity: 'success' | 'error' }) => void }

const SERVICE_CATEGORIES = [
  'Cloud Infrastructure', 'Data Processing', 'Identity & Access', 'Security',
  'Analytics', 'Payment Processing', 'HR & Payroll', 'CRM', 'Other',
]

const DATA_ACCESS_LEVELS = [
  'None', 'Public Data', 'Internal Data', 'Confidential Data', 'Restricted / PII',
]

const COMMON_CERTS = ['SOC 2 Type II', 'ISO 27001', 'PCI DSS', 'HIPAA BAA', 'FedRAMP', 'CSA STAR']

interface VendorForm {
  vendorName: string
  vendorWebsite: string
  serviceCategory: string
  dataAccessLevel: string
  contractValue: string
  certifications: string[]
  additionalContext: string
}

const EMPTY_FORM: VendorForm = {
  vendorName: '',
  vendorWebsite: '',
  serviceCategory: '',
  dataAccessLevel: '',
  contractValue: '',
  certifications: [],
  additionalContext: '',
}

function validate(form: VendorForm): Partial<Record<keyof VendorForm, string>> {
  const errors: Partial<Record<keyof VendorForm, string>> = {}
  if (!form.vendorName.trim()) errors.vendorName = 'Vendor name is required'
  if (!form.serviceCategory) errors.serviceCategory = 'Service category is required'
  if (!form.dataAccessLevel) errors.dataAccessLevel = 'Data access level is required'
  if (form.contractValue && isNaN(Number(form.contractValue))) errors.contractValue = 'Must be a valid number'
  return errors
}

export default function Vendors() {
  const { setSnackbar } = useOutletContext<OutletCtx>()
  const user = useAuthStore((s) => s.user)
  const eid = user!.enterpriseId
  const qc = useQueryClient()

  const [dialogOpen, setDialogOpen] = useState(false)
  const [form, setForm] = useState<VendorForm>(EMPTY_FORM)
  const [formErrors, setFormErrors] = useState<Partial<Record<keyof VendorForm, string>>>({})

  const vendors = useQuery({
    queryKey: ['vendors', eid],
    queryFn: () => vendorsApi.list(eid),
  })

  const behavioral = useQuery({
    queryKey: ['behavioral', eid],
    queryFn: () => behavioralApi.getRecent(eid),
  })

  useEffect(() => {
    if (vendors.error) setSnackbar({ open: true, message: getErrorMessage(vendors.error), severity: 'error' })
  }, [vendors.error])
  useEffect(() => {
    if (behavioral.error) setSnackbar({ open: true, message: getErrorMessage(behavioral.error), severity: 'error' })
  }, [behavioral.error])

  const assess = useMutation({
    mutationFn: () =>
      vendorsApi.assess({
        enterpriseId: eid,
        vendorName: form.vendorName,
        vendorWebsite: form.vendorWebsite || undefined,
        serviceCategory: form.serviceCategory,
        dataAccessLevel: form.dataAccessLevel,
        contractValue: form.contractValue ? Number(form.contractValue) : undefined,
        certifications: form.certifications.length ? form.certifications : undefined,
        additionalContext: form.additionalContext || undefined,
      }),
    onSuccess: () => {
      setSnackbar({ open: true, message: `Vendor "${form.vendorName}" assessed successfully.`, severity: 'success' })
      setDialogOpen(false)
      setForm(EMPTY_FORM)
      setFormErrors({})
      qc.invalidateQueries({ queryKey: ['vendors', eid] })
    },
    onError: (e: unknown) => setSnackbar({ open: true, message: getErrorMessage(e), severity: 'error' }),
  })

  function handleSubmit() {
    const errors = validate(form)
    setFormErrors(errors)
    if (Object.keys(errors).length > 0) return
    assess.mutate()
  }

  function toggleCert(cert: string) {
    setForm((f) => ({
      ...f,
      certifications: f.certifications.includes(cert)
        ? f.certifications.filter((c) => c !== cert)
        : [...f.certifications, cert],
    }))
  }

  const vendorList: VendorRiskSummary[] = vendors.data?.items ?? []
  const anomalies: BehavioralRiskEvent[] = (behavioral.data?.items ?? []).slice(0, 6)

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 3 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 700, letterSpacing: '-0.01em' }}>Vendor Risk</Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary', mt: 0.5 }}>
            Third-party vendor assessment and continuous risk monitoring
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => { setDialogOpen(true); setForm(EMPTY_FORM); setFormErrors({}) }}>
          Assess New Vendor
        </Button>
      </Box>

      {vendors.isLoading ? (
        <Grid container spacing={2.5} sx={{ mb: 3 }}>
          {[...Array(6)].map((_, i) => (
            <Grid key={i} item xs={12} sm={6} md={4}>
              <Skeleton variant="rectangular" height={200} sx={{ borderRadius: 2 }} />
            </Grid>
          ))}
        </Grid>
      ) : vendorList.length === 0 ? (
        <Paper sx={{ p: 6, textAlign: 'center', mb: 3 }}>
          <BusinessIcon sx={{ fontSize: 48, color: 'text.secondary', opacity: 0.4, mb: 1.5 }} />
          <Typography variant="h6" sx={{ fontWeight: 600, mb: 0.75 }}>No vendors assessed yet</Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary', mb: 2 }}>
            Click "Assess New Vendor" to add your first vendor risk assessment.
          </Typography>
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setDialogOpen(true)}>
            Assess New Vendor
          </Button>
        </Paper>
      ) : (
        <Grid container spacing={2.5} sx={{ mb: 3 }}>
          {vendorList.map((vendor) => {
            const color = RISK_LEVEL_COLORS[vendor.riskLevel as keyof typeof RISK_LEVEL_COLORS] ?? '#8B949E'
            return (
              <Grid key={vendor.vendorId} item xs={12} sm={6} md={4}>
                <Paper
                  sx={{
                    p: 2.5, height: '100%', display: 'flex', flexDirection: 'column', gap: 2,
                    borderTop: `3px solid ${color}`,
                    transition: 'box-shadow 0.2s',
                    '&:hover': { boxShadow: `0 4px 24px ${alpha(color, 0.15)}` },
                  }}
                >
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                      <Typography variant="subtitle1" sx={{ fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {vendor.vendorName}
                      </Typography>
                      <Typography variant="caption" sx={{ color: 'text.secondary' }}>{vendor.serviceCategory}</Typography>
                    </Box>
                    <RiskBadge riskLevel={vendor.riskLevel} score={vendor.compositeRiskScore} variant="gauge" />
                  </Box>

                  <Box>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                      <Typography variant="caption" sx={{ color: 'text.secondary' }}>Composite Risk</Typography>
                      <Typography variant="caption" sx={{ color, fontWeight: 600 }}>
                        {RISK_LEVEL_NAMES[vendor.riskLevel]}
                      </Typography>
                    </Box>
                    <LinearProgress
                      variant="determinate"
                      value={(vendor.compositeRiskScore / 4) * 100}
                      sx={{ height: 5, borderRadius: 3, '& .MuiLinearProgress-bar': { backgroundColor: color } }}
                    />
                  </Box>

                  {vendor.topRisks && vendor.topRisks.length > 0 && (
                    <Box>
                      <Typography variant="caption" sx={{ color: 'text.secondary', textTransform: 'uppercase', fontSize: '0.65rem', letterSpacing: '0.06em', mb: 0.75, display: 'block' }}>
                        Top Risks
                      </Typography>
                      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
                        {vendor.topRisks.slice(0, 3).map((risk: string, i: number) => (
                          <Box key={i} sx={{ display: 'flex', alignItems: 'flex-start', gap: 1 }}>
                            <Box sx={{ width: 5, height: 5, borderRadius: '50%', backgroundColor: color, flexShrink: 0, mt: 0.7 }} />
                            <Typography variant="caption" sx={{ color: 'text.secondary', lineHeight: 1.5, fontSize: '0.78rem' }}>
                              {risk}
                            </Typography>
                          </Box>
                        ))}
                      </Box>
                    </Box>
                  )}

                  <Divider />
                  <Typography variant="caption" sx={{ color: 'text.secondary', fontSize: '0.72rem' }}>
                    Assessed {format(new Date(vendor.lastAssessmentDate), 'MMM d, yyyy')}
                  </Typography>
                </Paper>
              </Grid>
            )
          })}
        </Grid>
      )}

      {/* Behavioral anomalies */}
      <Paper sx={{ p: 2.5 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 2 }}>Behavioral Anomalies</Typography>
        {behavioral.isLoading ? (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            {[...Array(4)].map((_, i) => <Skeleton key={i} variant="rectangular" height={52} sx={{ borderRadius: 1 }} />)}
          </Box>
        ) : anomalies.length === 0 ? (
          <Box sx={{ py: 4, textAlign: 'center' }}>
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>No recent behavioral anomalies detected</Typography>
          </Box>
        ) : (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.75 }}>
            {anomalies.map((event: BehavioralRiskEvent) => {
              const color = RISK_LEVEL_COLORS[event.riskLevel as keyof typeof RISK_LEVEL_COLORS] ?? '#8B949E'
              return (
                <Box
                  key={event.id}
                  sx={{
                    display: 'flex', alignItems: 'center', gap: 2, p: 1.5, borderRadius: 1.5,
                    border: '1px solid #21262D', borderLeft: `3px solid ${color}`,
                    backgroundColor: alpha('#0D1117', 0.4),
                  }}
                >
                  <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Typography variant="body2" sx={{ fontWeight: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {event.description}
                    </Typography>
                    <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                      {event.entityType} · {event.eventType} · {format(new Date(event.occurredAt), 'MMM d, h:mm a')}
                    </Typography>
                  </Box>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexShrink: 0 }}>
                    <Typography variant="caption" sx={{ color: 'text.secondary' }}>Score: {event.anomalyScore.toFixed(2)}</Typography>
                    <RiskBadge riskLevel={event.riskLevel} />
                    {event.isInvestigated && (
                      <Chip label="Investigated" size="small" sx={{ fontSize: '0.6rem', height: 18, color: '#3FB950', backgroundColor: alpha('#3FB950', 0.1) }} />
                    )}
                  </Box>
                </Box>
              )
            })}
          </Box>
        )}
      </Paper>

      {/* Assess Vendor Dialog */}
      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ fontWeight: 600 }}>Assess New Vendor</DialogTitle>
        <DialogContent sx={{ pt: '16px !important', display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            label="Vendor Name *"
            value={form.vendorName}
            onChange={(e) => setForm((f) => ({ ...f, vendorName: e.target.value }))}
            error={!!formErrors.vendorName}
            helperText={formErrors.vendorName}
            fullWidth
          />
          <TextField
            label="Vendor Website"
            value={form.vendorWebsite}
            onChange={(e) => setForm((f) => ({ ...f, vendorWebsite: e.target.value }))}
            fullWidth
            placeholder="https://vendor.example.com"
          />
          <FormControl fullWidth error={!!formErrors.serviceCategory}>
            <InputLabel>Service Category *</InputLabel>
            <Select value={form.serviceCategory} label="Service Category *" onChange={(e) => setForm((f) => ({ ...f, serviceCategory: e.target.value }))}>
              {SERVICE_CATEGORIES.map((c) => <MenuItem key={c} value={c}>{c}</MenuItem>)}
            </Select>
            {formErrors.serviceCategory && (
              <Typography variant="caption" sx={{ color: 'error.main', mt: 0.5, ml: 1.75 }}>{formErrors.serviceCategory}</Typography>
            )}
          </FormControl>
          <FormControl fullWidth error={!!formErrors.dataAccessLevel}>
            <InputLabel>Data Access Level *</InputLabel>
            <Select value={form.dataAccessLevel} label="Data Access Level *" onChange={(e) => setForm((f) => ({ ...f, dataAccessLevel: e.target.value }))}>
              {DATA_ACCESS_LEVELS.map((d) => <MenuItem key={d} value={d}>{d}</MenuItem>)}
            </Select>
            {formErrors.dataAccessLevel && (
              <Typography variant="caption" sx={{ color: 'error.main', mt: 0.5, ml: 1.75 }}>{formErrors.dataAccessLevel}</Typography>
            )}
          </FormControl>
          <TextField
            label="Contract Value (USD)"
            type="number"
            value={form.contractValue}
            onChange={(e) => setForm((f) => ({ ...f, contractValue: e.target.value }))}
            error={!!formErrors.contractValue}
            helperText={formErrors.contractValue}
            fullWidth
            inputProps={{ min: 0 }}
          />
          <Box>
            <Typography variant="caption" sx={{ color: 'text.secondary', mb: 1, display: 'block' }}>Certifications</Typography>
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.75 }}>
              {COMMON_CERTS.map((cert) => (
                <Chip
                  key={cert}
                  label={cert}
                  size="small"
                  clickable
                  icon={form.certifications.includes(cert) ? <VerifiedIcon sx={{ fontSize: '14px !important' }} /> : undefined}
                  onClick={() => toggleCert(cert)}
                  sx={{
                    backgroundColor: form.certifications.includes(cert) ? alpha('#3FB950', 0.15) : alpha('#ffffff', 0.04),
                    color: form.certifications.includes(cert) ? '#3FB950' : 'text.secondary',
                    border: `1px solid ${form.certifications.includes(cert) ? alpha('#3FB950', 0.4) : '#30363D'}`,
                    fontSize: '0.75rem',
                  }}
                />
              ))}
            </Box>
          </Box>
          <TextField
            label="Additional Context"
            value={form.additionalContext}
            onChange={(e) => setForm((f) => ({ ...f, additionalContext: e.target.value }))}
            fullWidth
            multiline
            rows={3}
            placeholder="Any additional context about this vendor relationship…"
          />
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2.5 }}>
          <Button onClick={() => setDialogOpen(false)} disabled={assess.isPending}>Cancel</Button>
          <Button
            variant="contained"
            onClick={handleSubmit}
            disabled={assess.isPending}
            startIcon={assess.isPending ? <CircularProgress size={14} color="inherit" /> : <BusinessIcon />}
          >
            {assess.isPending ? 'Assessing…' : 'Run Assessment'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}

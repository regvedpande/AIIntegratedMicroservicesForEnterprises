import { useState, useCallback, useEffect } from 'react'
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
  Drawer,
  IconButton,
  CircularProgress,
  Skeleton,
  LinearProgress,
  Chip,
} from '@mui/material'
import {
  CloudUpload as UploadIcon,
  Close as CloseIcon,
  Description as DocIcon,
  Warning as WarningIcon,
  CheckCircle as OkIcon,
  InsertDriveFile as FileIcon,
} from '@mui/icons-material'
import { alpha } from '@mui/material/styles'
import { DataGrid, GridColDef, GridRenderCellParams } from '@mui/x-data-grid'
import { useDropzone } from 'react-dropzone'
import { useQuery } from '@tanstack/react-query'
import { format } from 'date-fns'
import { useAuthStore } from '../store/auth'
import { documentsApi } from '../api/endpoints'
import { getErrorMessage } from '../api/client'
import RiskBadge from '../components/RiskBadge'
import {
  DOCUMENT_TYPE_NAMES,
  DOCUMENT_TYPE_ENUM,
  type DocumentAnalysisSummary,
  type DocumentAnalysisDetail,
} from '../types'

type OutletCtx = { setSnackbar: (s: { open: boolean; message: string; severity: 'success' | 'error' }) => void }

const DOCUMENT_TYPES = Object.entries(DOCUMENT_TYPE_ENUM)

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export default function Documents() {
  const { setSnackbar } = useOutletContext<OutletCtx>()
  const user = useAuthStore((s) => s.user)
  const eid = user!.enterpriseId

  const [selectedDocType, setSelectedDocType] = useState<number>(Object.values(DOCUMENT_TYPE_ENUM)[0])
  const [pendingFile, setPendingFile] = useState<File | null>(null)
  const [analyzing, setAnalyzing] = useState(false)
  const [drawerDoc, setDrawerDoc] = useState<DocumentAnalysisSummary | null>(null)
  const [refetchTrigger, setRefetchTrigger] = useState(0)

  const documents = useQuery({
    queryKey: ['documents', eid, refetchTrigger],
    queryFn: () => documentsApi.list(eid, { pageSize: 100 }),
  })

  const analysis = useQuery({
    queryKey: ['doc-analysis', drawerDoc?.documentId],
    queryFn: () => documentsApi.getAnalysis(drawerDoc!.documentId),
    enabled: !!drawerDoc,
  })

  useEffect(() => {
    if (documents.error) setSnackbar({ open: true, message: getErrorMessage(documents.error), severity: 'error' })
  }, [documents.error])
  useEffect(() => {
    if (analysis.error) setSnackbar({ open: true, message: getErrorMessage(analysis.error), severity: 'error' })
  }, [analysis.error])

  const onDrop = useCallback((accepted: File[]) => {
    if (accepted.length > 0) setPendingFile(accepted[0])
  }, [])

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    multiple: false,
    accept: {
      'application/pdf': ['.pdf'],
      'application/msword': ['.doc'],
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document': ['.docx'],
      'text/plain': ['.txt'],
    },
  })

  async function handleAnalyze() {
    if (!pendingFile) return
    setAnalyzing(true)
    try {
      await documentsApi.analyze(pendingFile, eid, selectedDocType)
      setSnackbar({ open: true, message: `"${pendingFile.name}" analyzed successfully.`, severity: 'success' })
      setPendingFile(null)
      setRefetchTrigger((t) => t + 1)
    } catch (e) {
      setSnackbar({ open: true, message: getErrorMessage(e), severity: 'error' })
    } finally {
      setAnalyzing(false)
    }
  }

  const columns: GridColDef[] = [
    {
      field: 'fileName',
      headerName: 'File Name',
      flex: 1,
      minWidth: 200,
      renderCell: (params: GridRenderCellParams) => (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <FileIcon sx={{ fontSize: 16, color: '#2F81F7' }} />
          <Typography variant="body2" sx={{ fontWeight: 500 }}>{params.value as string}</Typography>
        </Box>
      ),
    },
    {
      field: 'type',
      headerName: 'Type',
      width: 180,
      renderCell: (params: GridRenderCellParams) => (
        <Typography variant="body2" sx={{ color: 'text.secondary', fontSize: '0.82rem' }}>
          {DOCUMENT_TYPE_NAMES[params.value as number] ?? 'Unknown'}
        </Typography>
      ),
    },
    {
      field: 'overallRiskLevel',
      headerName: 'Risk Level',
      width: 120,
      renderCell: (params: GridRenderCellParams) => <RiskBadge riskLevel={params.value as number} />,
    },
    {
      field: 'riskScore',
      headerName: 'Risk Score',
      width: 130,
      renderCell: (params: GridRenderCellParams) => {
        const score = params.value as number
        const color = score >= 70 ? '#F85149' : score >= 40 ? '#D29922' : '#3FB950'
        return (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, width: '100%' }}>
            <LinearProgress
              variant="determinate"
              value={score}
              sx={{ flex: 1, height: 6, borderRadius: 3, '& .MuiLinearProgress-bar': { backgroundColor: color } }}
            />
            <Typography variant="caption" sx={{ color, fontWeight: 600, minWidth: 28 }}>
              {Math.round(score)}
            </Typography>
          </Box>
        )
      },
    },
    {
      field: 'findingsCount',
      headerName: 'Findings',
      width: 100,
      renderCell: (params: GridRenderCellParams) => (
        <Chip
          label={params.value as number}
          size="small"
          sx={{
            backgroundColor: (params.value as number) > 0 ? alpha('#D29922', 0.1) : alpha('#3FB950', 0.1),
            color: (params.value as number) > 0 ? '#D29922' : '#3FB950',
            fontWeight: 600,
            fontSize: '0.75rem',
          }}
        />
      ),
    },
    {
      field: 'complianceConcernsCount',
      headerName: 'Concerns',
      width: 100,
      renderCell: (params: GridRenderCellParams) => (
        <Chip
          label={params.value as number}
          size="small"
          sx={{
            backgroundColor: (params.value as number) > 0 ? alpha('#F85149', 0.1) : alpha('#3FB950', 0.1),
            color: (params.value as number) > 0 ? '#F85149' : '#3FB950',
            fontWeight: 600,
            fontSize: '0.75rem',
          }}
        />
      ),
    },
    {
      field: 'analyzedAt',
      headerName: 'Analyzed',
      width: 150,
      renderCell: (params: GridRenderCellParams) => (
        <Typography variant="body2" sx={{ color: 'text.secondary', fontSize: '0.8rem' }}>
          {format(new Date(params.value as string), 'MMM d, yyyy')}
        </Typography>
      ),
    },
  ]

  const analysisDetail = analysis.data as DocumentAnalysisDetail | undefined

  return (
    <Box>
      <Box sx={{ mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700, letterSpacing: '-0.01em' }}>Document Analysis</Typography>
        <Typography variant="body2" sx={{ color: 'text.secondary', mt: 0.5 }}>
          AI-powered risk analysis for contracts, policies, and compliance documents
        </Typography>
      </Box>

      {/* Upload zone */}
      <Paper sx={{ p: 2.5, mb: 2.5 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 2 }}>Analyze a Document</Typography>
        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'flex-start' }}>
          <Box
            {...getRootProps()}
            sx={{
              flex: '1 1 300px',
              minHeight: 120,
              border: `2px dashed ${isDragActive ? '#2F81F7' : '#30363D'}`,
              borderRadius: 2,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 1,
              cursor: 'pointer',
              transition: 'all 0.2s ease',
              backgroundColor: isDragActive ? alpha('#2F81F7', 0.06) : 'transparent',
              '&:hover': { borderColor: '#2F81F7', backgroundColor: alpha('#2F81F7', 0.04) },
              p: 3,
            }}
          >
            <input {...getInputProps()} />
            {pendingFile ? (
              <>
                <FileIcon sx={{ fontSize: 32, color: '#2F81F7' }} />
                <Typography variant="body2" sx={{ fontWeight: 500, color: 'text.primary', textAlign: 'center' }}>
                  {pendingFile.name}
                </Typography>
                <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                  {formatBytes(pendingFile.size)} · Click or drag to replace
                </Typography>
              </>
            ) : (
              <>
                <UploadIcon sx={{ fontSize: 32, color: isDragActive ? '#2F81F7' : 'text.secondary' }} />
                <Typography variant="body2" sx={{ fontWeight: 500, textAlign: 'center' }}>
                  {isDragActive ? 'Drop the file here' : 'Drag & drop a document, or click to browse'}
                </Typography>
                <Typography variant="caption" sx={{ color: 'text.secondary' }}>PDF, DOCX, DOC, TXT supported</Typography>
              </>
            )}
          </Box>

          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5, flex: '0 0 220px' }}>
            <FormControl fullWidth size="small">
              <InputLabel>Document Type</InputLabel>
              <Select
                value={selectedDocType}
                label="Document Type"
                onChange={(e) => setSelectedDocType(Number(e.target.value))}
              >
                {DOCUMENT_TYPES.map(([name, value]) => (
                  <MenuItem key={value} value={Number(value)}>
                    {name.replace(/([A-Z])/g, ' $1').trim()}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <Button
              variant="contained"
              startIcon={analyzing ? <CircularProgress size={14} color="inherit" /> : <UploadIcon />}
              onClick={handleAnalyze}
              disabled={!pendingFile || analyzing}
              fullWidth
            >
              {analyzing ? 'Analyzing…' : 'Analyze Document'}
            </Button>
            {pendingFile && (
              <Button variant="text" size="small" onClick={() => setPendingFile(null)} sx={{ color: 'text.secondary' }}>
                Clear
              </Button>
            )}
          </Box>
        </Box>
      </Paper>

      {/* Documents table */}
      <Paper sx={{ p: 0 }}>
        <Box sx={{ px: 2.5, py: 2, borderBottom: '1px solid #30363D', display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <DocIcon sx={{ color: 'text.secondary', fontSize: 18 }} />
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>Analyzed Documents</Typography>
          {!documents.isLoading && (
            <Chip label={documents.data?.totalCount ?? 0} size="small" sx={{ height: 20, fontSize: '0.7rem' }} />
          )}
        </Box>

        {documents.isLoading ? (
          <Box sx={{ p: 2.5, display: 'flex', flexDirection: 'column', gap: 1 }}>
            {[...Array(5)].map((_, i) => <Skeleton key={i} variant="rectangular" height={52} sx={{ borderRadius: 1 }} />)}
          </Box>
        ) : (
          <DataGrid
            rows={documents.data?.items ?? []}
            columns={columns}
            getRowId={(row) => (row as DocumentAnalysisSummary).documentId}
            autoHeight
            onRowClick={(params) => setDrawerDoc(params.row as DocumentAnalysisSummary)}
            disableRowSelectionOnClick
            pageSizeOptions={[25, 50]}
            initialState={{ pagination: { paginationModel: { pageSize: 25 } } }}
            sx={{ border: 'none', '& .MuiDataGrid-row': { cursor: 'pointer' } }}
            slots={{
              noRowsOverlay: () => (
                <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '100%', gap: 1, py: 6 }}>
                  <DocIcon sx={{ fontSize: 40, color: 'text.secondary', opacity: 0.4 }} />
                  <Typography variant="body1" sx={{ fontWeight: 500 }}>No documents analyzed yet</Typography>
                  <Typography variant="body2" sx={{ color: 'text.secondary' }}>Upload a document above to run AI risk analysis.</Typography>
                </Box>
              ),
            }}
          />
        )}
      </Paper>

      {/* Analysis Detail Drawer */}
      <Drawer
        anchor="right"
        open={!!drawerDoc}
        onClose={() => setDrawerDoc(null)}
        PaperProps={{ sx: { width: { xs: '100%', sm: 520 }, backgroundColor: '#161B22' } }}
      >
        {drawerDoc && (
          <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
            <Box sx={{ px: 3, py: 2.5, borderBottom: '1px solid #30363D', display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 2 }}>
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography variant="subtitle1" sx={{ fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {drawerDoc.fileName}
                </Typography>
                <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                  {DOCUMENT_TYPE_NAMES[drawerDoc.type]} · {format(new Date(drawerDoc.analyzedAt), 'MMM d, yyyy')}
                </Typography>
              </Box>
              <IconButton size="small" onClick={() => setDrawerDoc(null)}>
                <CloseIcon fontSize="small" />
              </IconButton>
            </Box>

            <Box sx={{ flex: 1, overflow: 'auto', px: 3, py: 2.5 }}>
              {analysis.isLoading ? (
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                  {[...Array(5)].map((_, i) => <Skeleton key={i} variant="rectangular" height={60} sx={{ borderRadius: 1 }} />)}
                </Box>
              ) : (
                <>
                  <Box sx={{ p: 2, borderRadius: 2, border: '1px solid #30363D', backgroundColor: alpha('#0D1117', 0.5), mb: 3, display: 'flex', alignItems: 'center', gap: 3 }}>
                    <RiskBadge riskLevel={drawerDoc.overallRiskLevel} score={drawerDoc.riskScore} variant="gauge" />
                    <Box sx={{ flex: 1 }}>
                      <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>Executive Summary</Typography>
                      <Typography variant="body2" sx={{ color: 'text.secondary', lineHeight: 1.6, fontSize: '0.82rem' }}>
                        {analysisDetail?.executiveSummary ?? drawerDoc.executiveSummary ?? 'No summary available.'}
                      </Typography>
                    </Box>
                  </Box>

                  <Box sx={{ display: 'flex', gap: 2, mb: 3 }}>
                    {[
                      { label: 'Findings', value: drawerDoc.findingsCount, icon: WarningIcon, color: '#D29922' },
                      { label: 'Compliance Concerns', value: drawerDoc.complianceConcernsCount, icon: WarningIcon, color: '#F85149' },
                    ].map(({ label, value, icon: Icon, color }) => (
                      <Box
                        key={label}
                        sx={{
                          flex: 1, p: 1.5, borderRadius: 1.5,
                          border: `1px solid ${alpha(color, 0.3)}`,
                          backgroundColor: alpha(color, 0.06),
                          display: 'flex', alignItems: 'center', gap: 1.5,
                        }}
                      >
                        <Icon sx={{ color, fontSize: 20 }} />
                        <Box>
                          <Typography variant="h6" sx={{ fontWeight: 700, color, lineHeight: 1 }}>{value}</Typography>
                          <Typography variant="caption" sx={{ color: 'text.secondary' }}>{label}</Typography>
                        </Box>
                      </Box>
                    ))}
                  </Box>

                  {analysisDetail?.findings && analysisDetail.findings.length > 0 && (
                    <>
                      <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1.5 }}>Findings</Typography>
                      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1, mb: 3 }}>
                        {analysisDetail.findings.map((f) => (
                          <Box key={f.id} sx={{ p: 1.5, borderRadius: 1.5, border: '1px solid #21262D', backgroundColor: alpha('#0D1117', 0.4) }}>
                            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 0.5 }}>
                              <Typography variant="caption" sx={{ color: '#58A6FF', fontWeight: 500 }}>{f.category}</Typography>
                              <RiskBadge riskLevel={f.severity} />
                            </Box>
                            <Typography variant="body2" sx={{ color: 'text.primary', fontSize: '0.82rem' }}>{f.description}</Typography>
                            {f.clause && (
                              <Typography variant="caption" sx={{ color: 'text.secondary', mt: 0.25, display: 'block' }}>
                                Clause: {f.clause}
                              </Typography>
                            )}
                          </Box>
                        ))}
                      </Box>
                    </>
                  )}

                  {analysisDetail?.complianceConcerns && analysisDetail.complianceConcerns.length > 0 && (
                    <>
                      <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1.5 }}>Compliance Concerns</Typography>
                      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1, mb: 3 }}>
                        {analysisDetail.complianceConcerns.map((c) => (
                          <Box key={c.id} sx={{ p: 1.5, borderRadius: 1.5, border: '1px solid #21262D', backgroundColor: alpha('#0D1117', 0.4) }}>
                            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
                              <Chip label={c.framework} size="small" sx={{ fontSize: '0.65rem', height: 18, color: '#2F81F7', backgroundColor: alpha('#2F81F7', 0.1) }} />
                              {c.articleReference && (
                                <Typography variant="caption" sx={{ color: 'text.secondary' }}>Art. {c.articleReference}</Typography>
                              )}
                            </Box>
                            <Typography variant="body2" sx={{ color: 'text.primary', fontSize: '0.82rem' }}>{c.description}</Typography>
                          </Box>
                        ))}
                      </Box>
                    </>
                  )}

                  {analysisDetail?.recommendations && analysisDetail.recommendations.length > 0 && (
                    <>
                      <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1.5 }}>Recommendations</Typography>
                      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.75 }}>
                        {analysisDetail.recommendations.map((rec, i) => (
                          <Box key={i} sx={{ display: 'flex', alignItems: 'flex-start', gap: 1.5 }}>
                            <OkIcon sx={{ color: '#3FB950', fontSize: 16, mt: 0.25, flexShrink: 0 }} />
                            <Typography variant="body2" sx={{ color: 'text.secondary', fontSize: '0.82rem', lineHeight: 1.6 }}>{rec}</Typography>
                          </Box>
                        ))}
                      </Box>
                    </>
                  )}
                </>
              )}
            </Box>
          </Box>
        )}
      </Drawer>
    </Box>
  )
}

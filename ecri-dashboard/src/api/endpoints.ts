import { apiClient } from './client'
import type {
  PagedResult,
  ComplianceFrameworkSummary,
  ViolationSummaryDto,
  DocumentAnalysisSummary,
  DocumentAnalysisDetail,
  VendorRiskSummary,
  VendorAssessmentRequest,
  BehavioralRiskEvent,
  BehavioralAnomalyRequest,
  AuditEntrySummary,
  AuditQueryParams,
  AuditReportRequest,
  Alert,
  HealthStatus,
  ComplianceCheckRequest,
  ResolveViolationRequest,
} from '../types'

// ─── Health ───────────────────────────────────────────────────────────────────

export const healthApi = {
  check: () =>
    apiClient.get<HealthStatus>('/health').then((r) => r.data),
}

// ─── Compliance ───────────────────────────────────────────────────────────────

export const complianceApi = {
  runCheck: (req: ComplianceCheckRequest) =>
    apiClient.post('/compliance/check', req).then((r) => r.data),

  getFrameworks: (enterpriseId: string) =>
    apiClient
      .get<ComplianceFrameworkSummary[]>(`/compliance/${enterpriseId}/frameworks`)
      .then((r) => r.data),

  getViolations: (
    enterpriseId: string,
    params?: { framework?: number; status?: number; page?: number; pageSize?: number }
  ) =>
    apiClient
      .get<PagedResult<ViolationSummaryDto>>(`/compliance/${enterpriseId}/violations`, {
        params,
      })
      .then((r) => r.data),

  resolveViolation: (violationId: string, req: ResolveViolationRequest) =>
    apiClient
      .patch(`/compliance/violations/${violationId}/resolve`, req)
      .then((r) => r.data),
}

// ─── Documents ────────────────────────────────────────────────────────────────

export const documentsApi = {
  analyze: async (file: File, enterpriseId: string, documentType: number) => {
    // Backend expects JSON with base64-encoded file content
    const buffer = await file.arrayBuffer()
    const bytes = new Uint8Array(buffer)
    let binary = ''
    for (let i = 0; i < bytes.byteLength; i++) binary += String.fromCharCode(bytes[i])
    const base64Content = btoa(binary)

    return apiClient
      .post<DocumentAnalysisSummary>('/documents/analyze', {
        enterpriseId,
        uploadedByUserId: '00000000-0000-0000-0000-000000000001',
        fileName: file.name,
        documentType,
        base64Content,
      })
      .then((r) => r.data)
  },

  getAnalysis: (documentId: string) =>
    apiClient
      .get<DocumentAnalysisDetail>(`/documents/${documentId}/analysis`)
      .then((r) => r.data),

  list: (enterpriseId: string, params?: { page?: number; pageSize?: number }) =>
    apiClient
      .get<PagedResult<DocumentAnalysisSummary>>(`/documents/${enterpriseId}/list`, { params })
      .then((r) => r.data),
}

// ─── Vendors ─────────────────────────────────────────────────────────────────

export const vendorsApi = {
  assess: (req: VendorAssessmentRequest) =>
    apiClient.post<VendorRiskSummary>('/risk/vendors/assess', req).then((r) => r.data),

  list: (enterpriseId: string) =>
    apiClient
      .get<PagedResult<VendorRiskSummary>>(`/risk/${enterpriseId}/vendors`)
      .then((r) => r.data),
}

// ─── Behavioral ───────────────────────────────────────────────────────────────

export const behavioralApi = {
  reportAnomaly: (req: BehavioralAnomalyRequest) =>
    apiClient.post('/risk/behavioral/anomaly', req).then((r) => r.data),

  getRecent: (enterpriseId: string) =>
    apiClient
      .get<PagedResult<BehavioralRiskEvent>>(`/risk/${enterpriseId}/behavioral/recent`)
      .then((r) => r.data),
}

// ─── Audit ────────────────────────────────────────────────────────────────────

export const auditApi = {
  query: (params: AuditQueryParams) => {
    const { enterpriseId, ...rest } = params
    return apiClient
      .get<PagedResult<AuditEntrySummary>>(`/audit/${enterpriseId}/query`, { params: rest })
      .then((r) => r.data)
  },

  generateReport: (req: AuditReportRequest) =>
    apiClient.post('/audit/report', req).then((r) => r.data),
}

// ─── Alerts ───────────────────────────────────────────────────────────────────

export const alertsApi = {
  getActive: (enterpriseId: string) =>
    apiClient
      .get<Alert[]>(`/notifications/${enterpriseId}/active`)
      .then((r) => r.data),

  acknowledge: (alertId: string) =>
    apiClient.patch(`/notifications/${alertId}/acknowledge`).then((r) => r.data),
}

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

type RawDocumentAnalysisResponse = {
  documentId: string
  overallRiskLevel: number
  riskScore: number
  executiveSummary: string
  analyzedAt?: string
  findings?: Array<{
    category: string
    description: string
    riskLevel: number
    clauseReference?: string
    recommendation?: string
  }>
  complianceConcerns?: string[]
  recommendations?: string[]
}

// ─── Health ───────────────────────────────────────────────────────────────────

export const healthApi = {
  check: () =>
    apiClient.get<HealthStatus>('/health').then((r) => r.data),
}

// ─── Compliance ───────────────────────────────────────────────────────────────

export const complianceApi = {
  runCheck: (req: ComplianceCheckRequest) =>
    apiClient
      .post('/compliance/check', buildComplianceCheckPayload(req))
      .then((r) => r.data),

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
      .patch(`/compliance/violations/${violationId}/resolve`, {
        violationId,
        resolvedByUserId: req.resolvedByUserId,
        resolutionNotes: req.resolutionNotes,
      })
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
      .get<RawDocumentAnalysisResponse>(`/documents/${documentId}/analysis`)
      .then((r) => normalizeDocumentAnalysis(r.data)),

  list: (enterpriseId: string, params?: { page?: number; pageSize?: number }) =>
    apiClient
      .get<PagedResult<DocumentAnalysisSummary>>(`/documents/${enterpriseId}/list`, { params })
      .then((r) => r.data),
}

// ─── Vendors ─────────────────────────────────────────────────────────────────

export const vendorsApi = {
  assess: (req: VendorAssessmentRequest) =>
    apiClient
      .post<VendorRiskSummary>('/risk/vendors/assess', {
        enterpriseId: req.enterpriseId,
        vendorName: req.vendorName,
        vendorDomain: toVendorDomain(req.vendorWebsite),
        serviceCategory: req.serviceCategory,
        country: 'United States',
        dataTypesShared: mapDataAccessLevel(req.dataAccessLevel),
        hasSignedDPA: false,
        hasSOC2: req.certifications?.includes('SOC 2 Type II') ?? false,
        hasISO27001: req.certifications?.includes('ISO 27001') ?? false,
        additionalContext: req.additionalContext ?? '',
      })
      .then((r) => r.data),

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

  acknowledge: (alertId: string, userId: string) =>
    apiClient.patch(`/notifications/${alertId}/acknowledge`, { userId }).then((r) => r.data),
}

function buildComplianceCheckPayload(req: ComplianceCheckRequest) {
  const scope = req.scope?.trim()
  return {
    enterpriseId: req.enterpriseId,
    framework: req.framework,
    resourceType: scope || 'EnterpriseScope',
    resourceId: scope || req.enterpriseId,
    resourceData: JSON.stringify(defaultComplianceResourceData(req.framework, scope)),
  }
}

function defaultComplianceResourceData(framework: number, scope?: string) {
  const shared = {
    scope: scope || 'all-resources',
    encryptionEnabled: false,
    supportsDataDeletion: false,
    breachDetected: false,
    notifiedDPA: false,
    financialReportApproved: false,
    internalControlsTested: false,
    roleBasedAccessEnabled: false,
    tlsEnabled: false,
    cardDataEncrypted: false,
    mfaEnabled: false,
  }

  switch (framework) {
    case 1:
      return { ...shared, collectsUnnecessaryData: true }
    case 2:
      return { ...shared, financialReportApproved: false, internalControlsTested: false }
    case 3:
      return { ...shared, roleBasedAccessEnabled: false, tlsEnabled: false }
    case 4:
      return { ...shared, cardDataEncrypted: false, mfaEnabled: false }
    default:
      return shared
  }
}

function normalizeDocumentAnalysis(data: RawDocumentAnalysisResponse): DocumentAnalysisDetail {
  const findings = (data.findings ?? []).map((finding, index) => ({
    id: `${data.documentId}-finding-${index}`,
    category: finding.category,
    description: finding.description,
    severity: finding.riskLevel,
    clause: finding.clauseReference,
  }))

  const complianceConcerns = (data.complianceConcerns ?? []).map((description, index) => ({
    id: `${data.documentId}-concern-${index}`,
    framework: extractFramework(description),
    description,
    articleReference: extractArticleReference(description),
  }))

  return {
    documentId: data.documentId,
    fileName: '',
    type: 99,
    overallRiskLevel: data.overallRiskLevel,
    riskScore: data.riskScore,
    executiveSummary: data.executiveSummary,
    findingsCount: findings.length,
    complianceConcernsCount: complianceConcerns.length,
    analyzedAt: data.analyzedAt ?? new Date().toISOString(),
    findings,
    complianceConcerns,
    recommendations: data.recommendations ?? [],
  }
}

function extractFramework(description: string): string {
  const upper = description.toUpperCase()
  if (upper.includes('GDPR')) return 'GDPR'
  if (upper.includes('HIPAA')) return 'HIPAA'
  if (upper.includes('SOX')) return 'SOX'
  if (upper.includes('PCI')) return 'PCI DSS'
  return 'General'
}

function extractArticleReference(description: string): string | undefined {
  const match = description.match(/(?:article|art\.|section|sec\.)\s*([A-Za-z0-9.-]+)/i)
  return match?.[1]
}

function toVendorDomain(vendorWebsite?: string): string {
  if (!vendorWebsite) return ''
  try {
    return new URL(vendorWebsite).hostname
  } catch {
    return vendorWebsite.replace(/^https?:\/\//i, '').split('/')[0]
  }
}

function mapDataAccessLevel(level: string): string[] {
  switch (level) {
    case 'Public Data':
      return ['Public']
    case 'Internal Data':
      return ['Internal']
    case 'Confidential Data':
      return ['Confidential']
    case 'Restricted / PII':
      return ['PII']
    default:
      return []
  }
}

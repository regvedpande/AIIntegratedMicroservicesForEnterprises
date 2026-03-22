// ─── Enums ────────────────────────────────────────────────────────────────────

export type ComplianceFramework = 'GDPR' | 'SOX' | 'HIPAA' | 'PCIDSS' | 'ISO27001' | 'CCPA' | 'NIST'

export type ViolationSeverity = 'Low' | 'Medium' | 'High' | 'Critical'
export type ViolationStatus = 'Open' | 'InRemediation' | 'Resolved' | 'Accepted' | 'FalsePositive'

export type RiskLevel = 'Negligible' | 'Low' | 'Medium' | 'High' | 'Critical'

export type DocumentType =
  | 'Contract'
  | 'Policy'
  | 'Invoice'
  | 'DataProcessingAgreement'
  | 'NDA'
  | 'SLA'
  | 'PrivacyNotice'
  | 'AuditReport'
  | 'Other'

export type AuditAction =
  | 'Created'
  | 'Updated'
  | 'Deleted'
  | 'Accessed'
  | 'Exported'
  | 'Shared'
  | 'PolicyViolationDetected'
  | 'ComplianceCheckRun'
  | 'ReportGenerated'
  | 'AlertSent'
  | 'UserLogin'
  | 'UserLogout'
  | 'PermissionChanged'

// ─── Generic ──────────────────────────────────────────────────────────────────

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

// ─── Auth ─────────────────────────────────────────────────────────────────────

export interface AuthUser {
  userId: string
  enterpriseId: string
  email: string
  role: string
  token: string
}

// ─── Compliance ───────────────────────────────────────────────────────────────

export interface ComplianceFrameworkSummary {
  framework: number
  totalRules: number
  violationsOpen: number
  violationsResolved: number
  complianceScore: number
  lastChecked: string
}

export interface ViolationSummaryDto {
  violationId: string
  ruleCode: string
  title: string
  severity: number
  status: number
  affectedResource: string
  detectedAt: string
}

export interface ComplianceCheckRequest {
  enterpriseId: string
  framework: number
  scope?: string
}

export interface ResolveViolationRequest {
  resolution: string
  resolvedBy: string
}

// ─── Documents ────────────────────────────────────────────────────────────────

export interface DocumentAnalysisSummary {
  documentId: string
  fileName: string
  type: number
  overallRiskLevel: number
  riskScore: number
  executiveSummary: string
  findingsCount: number
  complianceConcernsCount: number
  analyzedAt: string
}

export interface DocumentAnalysisDetail extends DocumentAnalysisSummary {
  findings: DocumentFinding[]
  complianceConcerns: ComplianceConcern[]
  recommendations: string[]
}

export interface DocumentFinding {
  id: string
  category: string
  description: string
  severity: number
  clause?: string
}

export interface ComplianceConcern {
  id: string
  framework: string
  description: string
  articleReference?: string
}

// ─── Vendors ─────────────────────────────────────────────────────────────────

export interface VendorRiskSummary {
  vendorId: string
  vendorName: string
  riskLevel: number
  compositeRiskScore: number
  serviceCategory: string
  lastAssessmentDate: string
  topRisks: string[]
}

export interface VendorAssessmentRequest {
  enterpriseId: string
  vendorName: string
  vendorWebsite?: string
  serviceCategory: string
  dataAccessLevel: string
  contractValue?: number
  certifications?: string[]
  additionalContext?: string
}

// ─── Behavioral Risk ─────────────────────────────────────────────────────────

export interface BehavioralRiskEvent {
  id: string
  entityId: string
  entityType: string
  eventType: string
  description: string
  riskLevel: number
  anomalyScore: number
  occurredAt: string
  isInvestigated: boolean
}

export interface BehavioralAnomalyRequest {
  enterpriseId: string
  entityId: string
  entityType: string
  eventData: Record<string, unknown>
}

// ─── Audit ────────────────────────────────────────────────────────────────────

export interface AuditEntrySummary {
  id: string
  userEmail: string
  action: number
  resourceType: string
  resourceName: string
  description: string
  ipAddress: string
  occurredAt: string
}

export interface AuditQueryParams {
  enterpriseId: string
  fromDate?: string
  toDate?: string
  action?: number
  userEmail?: string
  resourceType?: string
  page?: number
  pageSize?: number
}

export interface AuditReportRequest {
  enterpriseId: string
  fromDate: string
  toDate: string
  includeActions?: number[]
  format?: 'PDF' | 'CSV' | 'JSON'
}

// ─── Alerts ───────────────────────────────────────────────────────────────────

export interface Alert {
  id: string
  enterpriseId: string
  title: string
  message: string
  severity: number
  triggerSource: string
  isAcknowledged: boolean
  isResolved: boolean
  createdAt: string
}

// ─── Health ───────────────────────────────────────────────────────────────────

export interface HealthStatus {
  status: string
  timestamp: string
  services?: Record<string, string>
}

// ─── Lookup Maps ─────────────────────────────────────────────────────────────

export const FRAMEWORK_NAMES: Record<number, string> = {
  1: 'GDPR',
  2: 'SOX',
  3: 'HIPAA',
  4: 'PCI DSS',
  5: 'ISO 27001',
  6: 'CCPA',
  7: 'NIST',
}

export const FRAMEWORK_ENUM: Record<string, number> = {
  GDPR: 1,
  SOX: 2,
  HIPAA: 3,
  PCIDSS: 4,
  ISO27001: 5,
  CCPA: 6,
  NIST: 7,
}

export const SEVERITY_NAMES: Record<number, string> = {
  1: 'Low',
  2: 'Medium',
  3: 'High',
  4: 'Critical',
}

export const STATUS_NAMES: Record<number, string> = {
  1: 'Open',
  2: 'In Remediation',
  3: 'Resolved',
  4: 'Accepted',
  5: 'False Positive',
}

export const RISK_LEVEL_NAMES: Record<number, string> = {
  0: 'Negligible',
  1: 'Low',
  2: 'Medium',
  3: 'High',
  4: 'Critical',
}

export const DOCUMENT_TYPE_NAMES: Record<number, string> = {
  1: 'Contract',
  2: 'Policy',
  3: 'Invoice',
  4: 'Data Processing Agreement',
  5: 'NDA',
  6: 'SLA',
  7: 'Privacy Notice',
  8: 'Audit Report',
  99: 'Other',
}

export const DOCUMENT_TYPE_ENUM: Record<string, number> = {
  Contract: 1,
  Policy: 2,
  Invoice: 3,
  DataProcessingAgreement: 4,
  NDA: 5,
  SLA: 6,
  PrivacyNotice: 7,
  AuditReport: 8,
  Other: 99,
}

export const AUDIT_ACTION_NAMES: Record<number, string> = {
  1: 'Created',
  2: 'Updated',
  3: 'Deleted',
  4: 'Accessed',
  5: 'Exported',
  6: 'Shared',
  7: 'Policy Violation Detected',
  8: 'Compliance Check Run',
  9: 'Report Generated',
  10: 'Alert Sent',
  11: 'User Login',
  12: 'User Logout',
  13: 'Permission Changed',
}

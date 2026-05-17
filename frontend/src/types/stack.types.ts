// Enums
export enum ServerType {
  Standard = 'Standard',
  Playerbots = 'Playerbots',
}

export enum BuildPhase {
  Cloning = 'Cloning',
  PreparingModules = 'PreparingModules',
  Building = 'Building',
  CreatingImages = 'CreatingImages',
  Completed = 'Completed',
  Failed = 'Failed',
}

export enum StackStatus {
  Building = 'Building',
  Stopped = 'Stopped',
  Initializing = 'Initializing',
  Starting = 'Starting',
  Degraded = 'Degraded',
  Running = 'Running',
  Failed = 'Failed',
}

// Configuration DTOs
export interface DatabaseConfigDto {
  rootPassword: string
  port: number
}

export interface PortConfigDto {
  authServer: number
  worldServer: number
  soapPort: number
}

export interface AdvancedConfigDto {
  maxPlayers: number
  realmName: string
  customEnvVars?: Record<string, string>
}

export interface StackConfigurationDto {
  stackName: string
  serverType: ServerType
  moduleIds: string[]
  database: DatabaseConfigDto
  ports: PortConfigDto
  advanced: AdvancedConfigDto
}

// Build DTOs
export interface BuildStatusDto {
  buildId: string
  currentPhase: BuildPhase
  progressPercent: number
  currentStep: string
  recentLogs: string[]
  startedAt: string
  estimatedCompletion?: string
  errorMessage?: string
}

// Stack DTOs
export interface ContainerStatusDto {
  name: string
  status: string
  health: string
  startedAt: string
}

export interface ModuleVersionStatusDto {
  moduleId: string
  moduleName: string
  isOutdated: boolean
  currentCommitSha?: string
  latestCommitSha?: string
}

export interface DiscoveredStackDto {
  stackId: string
  suggestedName: string
  inferredServerType: ServerType
  currentStatus: StackStatus
  databasePort: number
  authServerPort: number
  worldServerPort: number
  soapPort: number
  isOrphaned: boolean
  containerNames: string[]
  coreRepositoryUrl?: string
  coreBranch?: string
  coreCommitSha?: string
  discoveredAt: string
  discoveredModules?: string[]
  discoveredDatabasePassword?: string
  discoveredSoapUsername?: string
  discoveredSoapPassword?: string
  discoveredEnvVars?: Record<string, string>
}

export interface ImportStackRequestDto {
  stackName: string
  databaseRootPassword?: string
  soapUsername?: string
  soapPassword?: string
}

export interface CiCheckDto {
  name: string
  status: string
  conclusion?: string
  htmlUrl?: string
}

export interface CiBuildStatusDto {
  status: string // "success", "failure", "pending", "unknown"
  criticalChecks: CiCheckDto[]
  checkedAt: string
  totalChecks: number
  passedChecks: number
  failedChecks: number
}

export interface StackUpdateStatusDto {
  stackId: string
  hasUpdates: boolean
  isCoreOutdated: boolean
  outdatedModuleCount: number
  currentCoreSha?: string
  latestCoreSha?: string
  outdatedModules: ModuleVersionStatusDto[]
  lastCheckedAt?: string
  latestCoreBuildStatus?: CiBuildStatusDto
}

export interface StackDetailsDto {
  stackId: string
  stackName: string
  serverType: ServerType
  status: StackStatus
  containers: ContainerStatusDto[]
  configuration: StackConfigurationDto
  createdAt: string
  updateStatus?: StackUpdateStatusDto
  isAdminAccountInitialized: boolean
  adminAccountInitializedAt?: string
}

export interface SoapCredentialsDto {
  username: string
  password: string
}

export interface InitializeAdminResponseDto {
  success: boolean
  created: boolean
  message: string
  username?: string
  password?: string
}

export interface StackListDto {
  stackId: string
  stackName: string
  serverType: ServerType
  status: StackStatus
  createdAt: string
}

// Module DTO
export interface ModuleDto {
  id: string
  name: string
  description: string
  repository: string
  branch: string
  requiresPlayerbots: boolean
}

// API Request/Response DTOs
export interface CreateStackRequest {
  configuration: StackConfigurationDto
}

export interface CreateStackResponse {
  stackId: string
  status: string
}

export interface BuildConfigurationDto {
  moduleIds: string[]
}

export interface BuildStartedResponse {
  buildId: string
  status: string
}

export interface ValidationResultDto {
  isValid: boolean
  errors: ValidationError[]
  suggestedPorts: SuggestedPorts
}

export interface ValidationError {
  field: string
  message: string
}

export type PortFieldPath =
  | 'database.port'
  | 'ports.authServer'
  | 'ports.worldServer'
  | 'ports.soapPort'

export type SuggestedPorts = Partial<Record<PortFieldPath, number>>

export interface CleanupResultDto {
  freedSpace: number
}

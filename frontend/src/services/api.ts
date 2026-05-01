import axios from 'axios'
import type { 
  StackConfigurationDto, 
  StackDetailsDto,
  BuildStatusDto,
  ModuleDto,
  ValidationResultDto,
  ServerType,
  StackUpdateStatusDto,
} from '@/types/stack.types'
import type { ModuleConfigSchema } from '@/types/moduleConfig'

const apiClient = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
})

// Add request interceptor for error handling
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('API Error:', error)
    return Promise.reject(error)
  }
)

export default apiClient

// Stack API
export const stackApi = {
  // List all stacks
  list: () => apiClient.get<StackDetailsDto[]>('/stacks'),
  
  // Get stack details
  get: (stackId: string) => apiClient.get<StackDetailsDto>(`/stacks/${stackId}`),
  
  // Create stack
  create: (config: StackConfigurationDto) => 
    apiClient.post<{ stackId: string; status: string }>('/stacks', config),
  
  // Update stack configuration
  updateConfig: (stackId: string, config: StackConfigurationDto) => 
    apiClient.put<StackDetailsDto>(`/stacks/${stackId}`, config),
  
  // Delete stack
  delete: (stackId: string) => apiClient.delete(`/stacks/${stackId}`),
  
  // Control operations
  start: (stackId: string) => apiClient.post(`/stacks/${stackId}/start`),
  stop: (stackId: string) => apiClient.post(`/stacks/${stackId}/stop`),
  restart: (stackId: string) => apiClient.post(`/stacks/${stackId}/restart`),
  
  // Update operations
  checkUpdates: (stackId: string) => 
    apiClient.post<StackUpdateStatusDto>(`/stacks/${stackId}/check-updates`),
  update: (stackId: string) => 
    apiClient.post<BuildStatusDto>(`/stacks/${stackId}/update`),
}

// Build API
export const buildApi = {
  // Start build (configuration optional for rebuilds)
  start: (stackId: string, config?: StackConfigurationDto) =>
    apiClient.post<{ buildId: string; status: string }>(`/stacks/${stackId}/build`, config || {}),
  
  // Get build status
  status: (stackId: string) => apiClient.get<BuildStatusDto>(`/stacks/${stackId}/build/status`),
  
  // Cancel build
  cancel: (stackId: string) => apiClient.post(`/stacks/${stackId}/build/cancel`),
  
  // Cleanup build files
  cleanup: (stackId: string) => 
    apiClient.delete<{ freedSpace: number }>(`/stacks/${stackId}/build/files`),
}

// Module API
export const moduleApi = {
  list: (serverType?: ServerType) => 
    apiClient.get<ModuleDto[]>('/modules', { params: { serverType } }),
  
  getConfig: (moduleId: string) =>
    apiClient.get<ModuleConfigSchema>(`/modules/${moduleId}/config`),
}

// Validation API
export const validationApi = {
  validate: (config: StackConfigurationDto, existingStackId?: string) =>
    apiClient.post<ValidationResultDto>(`/stacks/validate${existingStackId ? `?existingStackId=${existingStackId}` : ''}`, config),
}

// Health check
export const healthApi = {
  check: () => apiClient.get<{ status: string; timestamp: string }>('/health'),
}

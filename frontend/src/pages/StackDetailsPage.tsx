import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { stackApi, buildApi } from '@/services/api'
import { StackStatus } from '@/types/stack.types'
import { useState, useMemo } from 'react'
import { stackKeys } from '@/hooks/useStacks'
import EditStackConfigModal from '@/components/EditStackConfigModal'
import UpdateStackDialog from '@/components/UpdateStackDialog'
import AccountsTab from '@/components/accounts/AccountsTab'
import ModuleSetupWarnings from '@/components/modules/ModuleSetupWarnings'
import { CiBuildStatusBadge } from '@/components/CiBuildStatusBadge'
import { Eye, EyeOff, Copy } from 'lucide-react'

// Helper to format commit SHAs safely
const formatSha = (sha?: string | null): string => {
  if (!sha) return 'Not yet built'
  return sha.substring(0, 7)
}

// Helper to format relative time
const formatRelativeTime = (date: string | Date): string => {
  const now = new Date()
  const time = new Date(date)
  const diffMs = now.getTime() - time.getTime()
  const diffMinutes = Math.floor(diffMs / 60000)
  const diffHours = Math.floor(diffMs / 3600000)
  const diffDays = Math.floor(diffMs / 86400000)

  if (diffMinutes < 1) return 'just now'
  if (diffMinutes < 60) return `${diffMinutes} minute${diffMinutes > 1 ? 's' : ''} ago`
  if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`
  if (diffDays < 7) return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`
  return time.toLocaleDateString()
}

export default function StackDetailsPage() {
  const { stackId } = useParams<{ stackId: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false)
  const [showEditModal, setShowEditModal] = useState(false)
  const [showUpdateDialog, setShowUpdateDialog] = useState(false)
  const [recentLifecycleAction, setRecentLifecycleAction] = useState<number | null>(null)
  const [activeTab, setActiveTab] = useState<'overview' | 'accounts' | 'logs'>('overview')
  const [soapCredsVisible, setSoapCredsVisible] = useState(false)
  const [soapCopied, setSoapCopied] = useState<'username' | 'password' | null>(null)

  // Fetch stack details with auto-refresh every 5 seconds
  // Poll when: Running, Starting, Building, Degraded, Initializing, or within 30 seconds of a lifecycle action
  const { data: stack, isLoading, error } = useQuery({
    queryKey: stackKeys.detail(stackId!),
    queryFn: () => stackApi.get(stackId!).then(res => res.data),
    enabled: !!stackId,
    refetchInterval: (query) => {
      const status = query.state.data?.status
      const shouldPollForStatus = 
        status === StackStatus.Running || 
        status === StackStatus.Starting ||
        status === StackStatus.Building ||
        status === StackStatus.Degraded ||
        status === StackStatus.Initializing
      
      // Also poll for 30 seconds after any lifecycle action
      const shouldPollForRecent = recentLifecycleAction && 
        (Date.now() - recentLifecycleAction < 30000)
      
      return shouldPollForStatus || shouldPollForRecent ? 5000 : false
    },
  })

  // Calculate stack uptime based on earliest running container
  const stackUptime = useMemo(() => {
    if (!stack || stack.containers.length === 0) return null
    
    const runningContainers = stack.containers.filter(c => 
      c.status.toLowerCase().includes('running') || c.status.toLowerCase().includes('up')
    )
    
    if (runningContainers.length === 0) return null
    
    const earliestStart = runningContainers.reduce((earliest, container) => {
      const startTime = new Date(container.startedAt).getTime()
      return startTime < earliest ? startTime : earliest
    }, Infinity)
    
    const uptimeMs = Date.now() - earliestStart
    const hours = Math.floor(uptimeMs / (1000 * 60 * 60))
    const minutes = Math.floor((uptimeMs % (1000 * 60 * 60)) / (1000 * 60))
    
    if (hours > 0) {
      return `${hours}h ${minutes}m`
    }
    return `${minutes}m`
  }, [stack])

  // Lifecycle mutations - invalidate both detail and list queries
  const startMutation = useMutation({
    mutationFn: () => stackApi.start(stackId!),
    onSuccess: () => {
      setRecentLifecycleAction(Date.now())
      queryClient.invalidateQueries({ queryKey: stackKeys.detail(stackId!) })
      queryClient.invalidateQueries({ queryKey: stackKeys.lists() })
    },
  })

  const stopMutation = useMutation({
    mutationFn: () => stackApi.stop(stackId!),
    onSuccess: () => {
      setRecentLifecycleAction(Date.now())
      queryClient.invalidateQueries({ queryKey: stackKeys.detail(stackId!) })
      queryClient.invalidateQueries({ queryKey: stackKeys.lists() })
    },
  })

  const restartMutation = useMutation({
    mutationFn: () => stackApi.restart(stackId!),
    onSuccess: () => {
      setRecentLifecycleAction(Date.now())
      queryClient.invalidateQueries({ queryKey: stackKeys.detail(stackId!) })
      queryClient.invalidateQueries({ queryKey: stackKeys.lists() })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: () => stackApi.delete(stackId!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: stackKeys.lists() })
      navigate('/stacks')
    },
  })

  const rebuildMutation = useMutation({
    mutationFn: () => buildApi.start(stackId!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: stackKeys.lists() })
      navigate(`/stacks/${stackId}/build`)
    },
  })

  const checkUpdatesMutation = useMutation({
    mutationFn: () => stackApi.checkUpdates(stackId!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: stackKeys.detail(stackId!) })
      queryClient.invalidateQueries({ queryKey: stackKeys.lists() })
    },
  })

  const updateStackMutation = useMutation({
    mutationFn: async () => {
      // Stop stack if running
      if (stack?.status === StackStatus.Running) {
        await stackApi.stop(stackId!)
        // Small delay to ensure stop is processed
        await new Promise(resolve => setTimeout(resolve, 500))
      }
      // Now trigger update
      return stackApi.update(stackId!)
    },
    onSuccess: () => {
      setShowUpdateDialog(false)
      queryClient.invalidateQueries({ queryKey: stackKeys.lists() })
      // Redirect to build page to show progress
      navigate(`/stacks/${stackId}/build`)
    },
    onError: (error) => {
      console.error('Update failed:', error)
      // User will see error in the dialog
    },
  })

  const soapCredentialsQuery = useQuery({
    queryKey: [...stackKeys.detail(stackId!), 'soap-credentials'],
    queryFn: () => stackApi.getSoapCredentials(stackId!),
    enabled: !!stackId && !!stack?.isAdminAccountInitialized,
    select: (res) => res.data,
  })

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-96">
        <div className="text-lg text-gray-600">Loading stack details...</div>
      </div>
    )
  }

  if (error || !stack) {
    return (
      <div className="max-w-2xl mx-auto mt-12">
        <div className="bg-red-50 border border-red-200 rounded-lg p-6">
          <h2 className="text-xl font-semibold text-red-800 mb-2">Stack Not Found</h2>
          <p className="text-red-600 mb-4">
            The stack you're looking for doesn't exist or has been deleted.
          </p>
          <button
            onClick={() => navigate('/stacks')}
            className="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700 transition"
          >
            Back to Stacks
          </button>
        </div>
      </div>
    )
  }

  const getStatusColor = (status: StackStatus) => {
    switch (status) {
      case StackStatus.Running:
        return 'bg-green-100 text-green-800 border-green-200'
      case StackStatus.Initializing:
        return 'bg-blue-100 text-blue-800 border-blue-200'
      case StackStatus.Starting:
        return 'bg-yellow-100 text-yellow-800 border-yellow-200'
      case StackStatus.Degraded:
        return 'bg-orange-100 text-orange-800 border-orange-200'
      case StackStatus.Stopped:
        return 'bg-gray-100 text-gray-800 border-gray-200'
      case StackStatus.Building:
        return 'bg-blue-100 text-blue-800 border-blue-200'
      case StackStatus.Failed:
        return 'bg-red-100 text-red-800 border-red-200'
      default:
        return 'bg-gray-100 text-gray-800 border-gray-200'
    }
  }

  const getContainerStatusColor = (status: string) => {
    if (status.toLowerCase().includes('running') || status.toLowerCase().includes('up')) {
      return 'text-green-600'
    }
    if (status.toLowerCase().includes('exited')) {
      return 'text-gray-600'
    }
    return 'text-yellow-600'
  }

  const getHealthIcon = (health: string) => {
    if (health === 'healthy') return '✓'
    if (health === 'unhealthy') return '✗'
    return '○'
  }

  const canStart = stack.status === StackStatus.Stopped || stack.status === StackStatus.Failed
  const canStop = stack.status === StackStatus.Running || stack.status === StackStatus.Starting || stack.status === StackStatus.Degraded || stack.status === StackStatus.Initializing
  const canRestart = stack.status === StackStatus.Running || stack.status === StackStatus.Degraded
  const isTransitioning = startMutation.isPending || stopMutation.isPending || restartMutation.isPending

  return (
    <div className="max-w-6xl mx-auto">
      {/* Header */}
      <div className="mb-8">
        <div className="flex items-center justify-between mb-4">
          <div>
            <button
              onClick={() => navigate('/stacks')}
              className="text-sm text-gray-600 hover:text-gray-800 mb-2 inline-flex items-center gap-1"
            >
              ← Back to Stacks
            </button>
            <h1 className="text-3xl font-bold text-gray-900">{stack.stackName}</h1>
            <div className="flex items-center gap-3 mt-1">
              <p className="text-sm text-gray-500">
                Created {new Date(stack.createdAt).toLocaleDateString()} • {stack.serverType}
              </p>
              {stackUptime && (
                <>
                  <span className="text-gray-300">•</span>
                  <p className="text-sm text-green-600 font-medium">
                    Uptime: {stackUptime}
                  </p>
                </>
              )}
            </div>
          </div>
          <div>
            <span className={`px-4 py-2 rounded-full text-sm font-medium border ${getStatusColor(stack.status)}`}>
              {stack.status}
            </span>
          </div>
        </div>

        {/* Lifecycle Controls */}
        <div className="flex gap-3 flex-wrap">
          <button
            onClick={() => startMutation.mutate()}
            disabled={!canStart || isTransitioning}
            className="px-4 py-2 bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed transition"
          >
            {startMutation.isPending ? 'Starting...' : 'Start'}
          </button>
          <button
            onClick={() => stopMutation.mutate()}
            disabled={!canStop || isTransitioning}
            className="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed transition"
          >
            {stopMutation.isPending ? 'Stopping...' : 'Stop'}
          </button>
          <button
            onClick={() => restartMutation.mutate()}
            disabled={!canRestart || isTransitioning}
            className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition"
          >
            {restartMutation.isPending ? 'Restarting...' : 'Restart'}
          </button>
          
          <div className="flex-1"></div>
          <button
            onClick={() => checkUpdatesMutation.mutate()}
            disabled={checkUpdatesMutation.isPending}
            className="px-4 py-2 border border-blue-300 text-blue-700 rounded hover:bg-blue-50 disabled:opacity-50 disabled:cursor-not-allowed transition"
            title="Check for updates to this stack"
          >
            {checkUpdatesMutation.isPending ? 'Checking...' : 'Check for Updates'}
          </button>
          <button
            onClick={() => setShowEditModal(true)}
            disabled={stack.status === StackStatus.Building}
            className="px-4 py-2 border border-blue-300 text-blue-700 rounded hover:bg-blue-50 disabled:opacity-50 disabled:cursor-not-allowed transition"
          >
            Edit Configuration
          </button>
          <button
            onClick={() => rebuildMutation.mutate()}
            disabled={stack.status === StackStatus.Building}
            className="px-4 py-2 border border-amber-300 text-amber-700 rounded hover:bg-amber-50 disabled:opacity-50 disabled:cursor-not-allowed transition"
          >
            {rebuildMutation.isPending ? 'Starting Rebuild...' : 'Rebuild'}
          </button>
          <button
            onClick={() => setShowDeleteConfirm(true)}
            disabled={deleteMutation.isPending}
            className="px-4 py-2 border border-red-300 text-red-700 rounded hover:bg-red-50 disabled:opacity-50 disabled:cursor-not-allowed transition"
          >
            Delete
          </button>
        </div>
      </div>

      {/* Tabs Navigation */}
      <div className="border-b border-gray-200 mb-6">
        <nav className="flex gap-6">
          <button
            onClick={() => setActiveTab('overview')}
            className={`pb-3 px-1 border-b-2 font-medium text-sm transition-colors ${
              activeTab === 'overview'
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            Overview
          </button>
          <button
            onClick={() => setActiveTab('accounts')}
            className={`pb-3 px-1 border-b-2 font-medium text-sm transition-colors ${
              activeTab === 'accounts'
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            Accounts
          </button>
          <button
            onClick={() => setActiveTab('logs')}
            className={`pb-3 px-1 border-b-2 font-medium text-sm transition-colors ${
              activeTab === 'logs'
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            Logs
          </button>
        </nav>
      </div>

      {/* Tab Content */}
      {activeTab === 'accounts' && (
        <AccountsTab stackId={stackId!} />
      )}

      {activeTab === 'logs' && (
        <div className="mb-8">
          <h2 className="text-xl font-semibold mb-4">Container Logs</h2>
          {stack.containers.length === 0 ? (
            <div className="bg-gray-50 border border-gray-200 rounded-lg p-6 text-center">
              <p className="text-gray-600">No containers running. Start the stack to see containers.</p>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {stack.containers.map((container) => (
                <div 
                  key={container.name} 
                  onClick={() => navigate(`/stacks/${stackId}/containers/${encodeURIComponent(container.name)}/logs`)}
                  className="bg-white border border-gray-200 rounded-lg p-4 shadow-sm cursor-pointer hover:border-blue-500 hover:shadow-md transition-all"
                >
                  <div className="flex items-start justify-between mb-2">
                    <h3 className="font-medium text-gray-900 text-sm truncate" title={container.name}>
                      {container.name.split('-').pop() || container.name}
                    </h3>
                    <span className="text-lg ml-2" title={`Health: ${container.health}`}>
                      {getHealthIcon(container.health)}
                    </span>
                  </div>
                  <div className="space-y-1 text-sm">
                    <div className="flex items-center gap-2">
                      <span className="text-gray-500">Status:</span>
                      <span className={`font-medium ${getContainerStatusColor(container.status)}`}>
                        {container.status}
                      </span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-gray-500">Started:</span>
                      <span className="text-gray-700">
                        {new Date(container.startedAt).toLocaleTimeString()}
                      </span>
                    </div>
                  </div>
                  <div className="mt-3 text-xs text-blue-600 font-medium">
                    Click to view logs →
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {activeTab === 'overview' && (
        <>
      {/* Module setup warnings */}
      <ModuleSetupWarnings stack={stack} />

      {/* Updates Available Section */}
      {stack.updateStatus?.hasUpdates && (
        <div className="mb-8">
          <div className="bg-amber-50 border border-amber-200 rounded-lg p-6">
            <div className="flex items-start justify-between mb-4">
              <div className="flex-1">
                <h2 className="text-xl font-semibold text-amber-900 mb-2">Updates Available</h2>
                <p className="text-sm text-amber-800 mb-3">
                  New versions are available for this stack. Update to get the latest features and bug fixes.
                </p>
                {/* CI Build Status Badge */}
                {stack.updateStatus.latestCoreBuildStatus && (
                  <div className="mb-3">
                    <CiBuildStatusBadge 
                      status={stack.updateStatus.latestCoreBuildStatus} 
                      showDetails={false}
                    />
                  </div>
                )}
              </div>
              <button
                onClick={() => setShowUpdateDialog(true)}
                disabled={stack.status === StackStatus.Building}
                className="px-3 py-1.5 text-sm bg-amber-600 text-white rounded hover:bg-amber-700 disabled:opacity-50 transition ml-4"
                title={stack.status === StackStatus.Building ? 'Wait for build to finish' : 'Update stack'}
              >
                Update Stack
              </button>
            </div>

            <div className="space-y-3 text-sm">
              {stack.updateStatus.isCoreOutdated && (
                <div className="flex items-start gap-2">
                  <span className="text-amber-600 mt-0.5">●</span>
                  <div className="flex-1">
                    <div className="font-medium text-amber-900">AzerothCore Server</div>
                    <div className="text-amber-700 text-xs font-mono mt-1">
                      {formatSha(stack.updateStatus.currentCoreSha)} → {formatSha(stack.updateStatus.latestCoreSha)}
                    </div>
                  </div>
                </div>
              )}

              {stack.updateStatus.outdatedModules.map((module) => (
                <div key={module.moduleId} className="flex items-start gap-2">
                  <span className="text-amber-600 mt-0.5">●</span>
                  <div className="flex-1">
                    <div className="font-medium text-amber-900">{module.moduleName}</div>
                    <div className="text-amber-700 text-xs font-mono mt-1">
                      {formatSha(module.currentCommitSha)} → {formatSha(module.latestCommitSha)}
                    </div>
                  </div>
                </div>
              ))}

              {stack.updateStatus.lastCheckedAt && (
                <div className="text-xs text-amber-700 pt-2 border-t border-amber-200 flex items-center justify-between">
                  <span>Last checked: {formatRelativeTime(stack.updateStatus.lastCheckedAt)}</span>
                  {checkUpdatesMutation.isPending && (
                    <span className="flex items-center gap-1">
                      <span className="inline-block w-2 h-2 bg-blue-500 rounded-full animate-pulse"></span>
                      Checking...
                    </span>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Containers Section */}
      <div className="mb-8">
        <h2 className="text-xl font-semibold mb-4">Containers</h2>
        {stack.containers.length === 0 ? (
          <div className="bg-gray-50 border border-gray-200 rounded-lg p-6 text-center">
            <p className="text-gray-600">No containers running. Start the stack to see container status.</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {stack.containers.map((container) => (
              <div 
                key={container.name} 
                onClick={() => navigate(`/stacks/${stackId}/containers/${encodeURIComponent(container.name)}/logs`)}
                className="bg-white border border-gray-200 rounded-lg p-4 shadow-sm cursor-pointer hover:border-blue-500 hover:shadow-md transition-all"
              >
                <div className="flex items-start justify-between mb-2">
                  <h3 className="font-medium text-gray-900 text-sm truncate" title={container.name}>
                    {container.name.split('-').pop() || container.name}
                  </h3>
                  <span className="text-lg ml-2" title={`Health: ${container.health}`}>
                    {getHealthIcon(container.health)}
                  </span>
                </div>
                <div className="space-y-1 text-sm">
                  <div className="flex items-center gap-2">
                    <span className="text-gray-500">Status:</span>
                    <span className={`font-medium ${getContainerStatusColor(container.status)}`}>
                      {container.status}
                    </span>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-gray-500">Started:</span>
                    <span className="text-gray-700">
                      {new Date(container.startedAt).toLocaleTimeString()}
                    </span>
                  </div>
                </div>
                <div className="mt-3 text-xs text-blue-600 font-medium">
                  Click to view logs →
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Configuration Section */}
      <div className="mb-8">
        <h2 className="text-xl font-semibold mb-4">Configuration</h2>
        <div className="bg-white border border-gray-200 rounded-lg p-6 space-y-6">
          {/* Database */}
          <div>
            <h3 className="font-medium text-gray-900 mb-2">Database</h3>
            <div className="grid grid-cols-2 gap-4 text-sm">
              <div>
                <span className="text-gray-500">Port:</span>
                <span className="ml-2 font-mono text-gray-900">{stack.configuration.database.port}</span>
              </div>
            </div>
          </div>

          {/* Ports */}
          <div>
            <h3 className="font-medium text-gray-900 mb-2">Server Ports</h3>
            <div className="grid grid-cols-2 md:grid-cols-3 gap-4 text-sm">
              <div>
                <span className="text-gray-500">Auth Server:</span>
                <span className="ml-2 font-mono text-gray-900">{stack.configuration.ports.authServer}</span>
              </div>
              <div>
                <span className="text-gray-500">World Server:</span>
                <span className="ml-2 font-mono text-gray-900">{stack.configuration.ports.worldServer}</span>
              </div>
              <div>
                <span className="text-gray-500">SOAP:</span>
                <span className="ml-2 font-mono text-gray-900">{stack.configuration.ports.soapPort}</span>
              </div>
            </div>
          </div>

          {/* Advanced */}
          <div>
            <h3 className="font-medium text-gray-900 mb-2">Advanced Settings</h3>
            <div className="grid grid-cols-2 gap-4 text-sm">
              <div>
                <span className="text-gray-500">Max Players:</span>
                <span className="ml-2 text-gray-900">{stack.configuration.advanced.maxPlayers}</span>
              </div>
              <div>
                <span className="text-gray-500">Realm Name:</span>
                <span className="ml-2 text-gray-900">{stack.configuration.advanced.realmName}</span>
              </div>
            </div>
          </div>

          {/* SOAP Credentials Recovery */}
          {stack.isAdminAccountInitialized && (
            <div>
              <h3 className="font-medium text-gray-900 mb-2">SOAP Admin Credentials</h3>
              <div className="space-y-2 text-sm">
                {soapCredentialsQuery.data ? (
                  <>
                    <div className="flex items-center gap-2 bg-gray-50 border border-gray-200 rounded-md px-3 py-2">
                      <span className="text-gray-500 w-20 shrink-0">Username</span>
                      <code className="flex-1 font-mono text-gray-900">{soapCredentialsQuery.data.username}</code>
                      <button
                        onClick={async () => {
                          await navigator.clipboard.writeText(soapCredentialsQuery.data!.username)
                          setSoapCopied('username')
                          setTimeout(() => setSoapCopied(null), 2000)
                        }}
                        className="text-gray-400 hover:text-gray-600 transition-colors"
                        title="Copy username"
                      >
                        <Copy className="h-4 w-4" />
                      </button>
                      {soapCopied === 'username' && <span className="text-xs text-green-600">Copied!</span>}
                    </div>
                    <div className="flex items-center gap-2 bg-gray-50 border border-gray-200 rounded-md px-3 py-2">
                      <span className="text-gray-500 w-20 shrink-0">Password</span>
                      <code className="flex-1 font-mono text-gray-900 break-all">
                        {soapCredsVisible ? soapCredentialsQuery.data.password : '•'.repeat(32)}
                      </code>
                      <button
                        onClick={() => setSoapCredsVisible(v => !v)}
                        className="text-gray-400 hover:text-gray-600 transition-colors"
                        title={soapCredsVisible ? 'Hide password' : 'Reveal password'}
                      >
                        {soapCredsVisible ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                      </button>
                      <button
                        onClick={async () => {
                          await navigator.clipboard.writeText(soapCredentialsQuery.data!.password)
                          setSoapCopied('password')
                          setTimeout(() => setSoapCopied(null), 2000)
                        }}
                        className="text-gray-400 hover:text-gray-600 transition-colors"
                        title="Copy password"
                      >
                        <Copy className="h-4 w-4" />
                      </button>
                      {soapCopied === 'password' && <span className="text-xs text-green-600">Copied!</span>}
                    </div>
                  </>
                ) : (
                  <p className="text-gray-500 text-sm italic">Loading credentials…</p>
                )}
              </div>
            </div>
          )}

          {/* Modules */}
          <div>
            <h3 className="font-medium text-gray-900 mb-2">Modules</h3>
            {stack.configuration.moduleIds.length === 0 ? (
              <p className="text-sm text-gray-500">No modules installed</p>
            ) : (
              <div className="flex flex-wrap gap-2">
                {stack.configuration.moduleIds.map((moduleId) => (
                  <span key={moduleId} className="px-3 py-1 bg-blue-50 text-blue-700 rounded-full text-sm">
                    {moduleId}
                  </span>
                ))}
              </div>
            )}
          </div>

          {/* Custom Env Vars */}
          {stack.configuration.advanced.customEnvVars && Object.keys(stack.configuration.advanced.customEnvVars).length > 0 && (
            <div>
              <h3 className="font-medium text-gray-900 mb-2">Custom Environment Variables</h3>
              <div className="bg-gray-50 rounded p-3 font-mono text-sm space-y-1">
                {Object.entries(stack.configuration.advanced.customEnvVars).map(([key, value]) => (
                  <div key={key}>
                    <span className="text-gray-600">{key}</span>=<span className="text-gray-900">{value}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
        </>
      )}

      {/* Delete Confirmation Modal */}
      {showDeleteConfirm && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6 max-w-md mx-4">
            <h3 className="text-xl font-semibold mb-4">Delete Stack?</h3>
            <p className="text-gray-600 mb-6">
              Are you sure you want to delete <strong>{stack.stackName}</strong>? 
              This will remove all containers, images, and build files. This action cannot be undone.
            </p>
            <div className="flex gap-3 justify-end">
              <button
                onClick={() => setShowDeleteConfirm(false)}
                className="px-4 py-2 border border-gray-300 rounded hover:bg-gray-50 transition"
              >
                Cancel
              </button>
              <button
                onClick={() => {
                  deleteMutation.mutate()
                  setShowDeleteConfirm(false)
                }}
                disabled={deleteMutation.isPending}
                className="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50 transition"
              >
                {deleteMutation.isPending ? 'Deleting...' : 'Delete Stack'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Edit Configuration Modal */}
      {showEditModal && (
        <EditStackConfigModal
          stack={stack}
          onClose={() => setShowEditModal(false)}
        />
      )}

      {/* Update Stack Dialog */}
      {showUpdateDialog && stack.updateStatus && (
        <UpdateStackDialog
          stackName={stack.stackName}
          updateStatus={stack.updateStatus}
          onConfirm={() => updateStackMutation.mutate()}
          onCancel={() => setShowUpdateDialog(false)}
          isUpdating={updateStackMutation.isPending}
        />
      )}
    </div>
  )
}

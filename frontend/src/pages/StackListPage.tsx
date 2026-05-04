import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Play, Square, RefreshCw, Plus, Loader2, Trash2, Hammer, AlertCircle, Download } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import { useState } from 'react'
import DeleteStackDialog from '@/components/DeleteStackDialog'
import { ImportStacksDialog } from '@/components/ImportStacksDialog'
import { useStacks } from '@/hooks/useStacks'
import { stackApi, buildApi } from '@/services/api'
import type { StackDetailsDto } from '@/types/stack.types'
import { StackStatus } from '@/types/stack.types'

// Calculate stack uptime from containers
function calculateStackUptime(stack: StackDetailsDto): string | null {
  const runningContainers = stack.containers.filter(c => 
    c.status.toLowerCase().includes('running') || c.status.toLowerCase().includes('up')
  )
  
  if (runningContainers.length === 0) return null
  
  // Find the earliest start time
  const earliestStart = runningContainers.reduce((earliest, container) => {
    const startTime = new Date(container.startedAt).getTime()
    return startTime < earliest ? startTime : earliest
  }, new Date(runningContainers[0].startedAt).getTime())
  
  const uptimeMs = Date.now() - earliestStart
  const uptimeMinutes = Math.floor(uptimeMs / 60000)
  const uptimeHours = Math.floor(uptimeMinutes / 60)
  const remainingMinutes = uptimeMinutes % 60
  
  if (uptimeHours > 0) {
    return `${uptimeHours}h ${remainingMinutes}m`
  } else {
    return `${uptimeMinutes}m`
  }
}

function getStatusBadgeColor(status: StackStatus): string {
  switch (status) {
    case StackStatus.Running:
      return 'bg-green-100 text-green-800'
    case StackStatus.Initializing:
      return 'bg-blue-100 text-blue-800'
    case StackStatus.Starting:
      return 'bg-yellow-100 text-yellow-800'
    case StackStatus.Degraded:
      return 'bg-orange-100 text-orange-800'
    case StackStatus.Stopped:
      return 'bg-gray-100 text-gray-800'
    case StackStatus.Building:
      return 'bg-blue-100 text-blue-800'
    case StackStatus.Failed:
      return 'bg-red-100 text-red-800'
    default:
      return 'bg-gray-100 text-gray-800'
  }
}

export default function StackListPage() {
  const navigate = useNavigate()
  const { data: stacks = [], isLoading } = useStacks()
  const queryClient = useQueryClient()
  const [deletingStack, setDeletingStack] = useState<{ id: string; name: string } | null>(null)
  const [importDialogOpen, setImportDialogOpen] = useState(false)

  const startStack = useMutation({
    mutationFn: (stackId: string) => stackApi.start(stackId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['stacks'] })
    },
  })

  const stopStack = useMutation({
    mutationFn: (stackId: string) => stackApi.stop(stackId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['stacks'] })
    },
  })

  const restartStack = useMutation({
    mutationFn: (stackId: string) => stackApi.restart(stackId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['stacks'] })
    },
  })

  const deleteStack = useMutation({
    mutationFn: (stackId: string) => stackApi.delete(stackId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['stacks'] })
      setDeletingStack(null)
    },
  })

  const rebuildStack = useMutation({
    mutationFn: (stackId: string) => buildApi.start(stackId), // No config = rebuild with existing config
    onSuccess: (_data, stackId) => {
      queryClient.invalidateQueries({ queryKey: ['stacks'] })
      // Navigate to build progress page
      navigate(`/stacks/${stackId}/build`)
    },
  })

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="h-8 w-8 animate-spin text-gray-400" />
      </div>
    )
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">Your Stacks</h1>
          <p className="mt-1 text-gray-500">Manage your AzerothCore server instances</p>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={() => setImportDialogOpen(true)}
            className="flex items-center gap-2 rounded-md bg-white border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            <Download className="h-4 w-4" />
            Import Existing Stacks
          </button>
          <Link
            to="/stacks/new"
            className="flex items-center gap-2 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            <Plus className="h-4 w-4" />
            Create Stack
          </Link>
        </div>
      </div>

      {stacks.length === 0 ? (
        <div className="rounded-lg border-2 border-dashed border-gray-300 p-12 text-center">
          <h3 className="text-lg font-medium text-gray-900">No stacks yet</h3>
          <p className="mt-2 text-gray-500">Get started by creating your first AzerothCore server stack or import an existing one.</p>
          <div className="mt-4 flex items-center justify-center gap-3">
            <button
              onClick={() => setImportDialogOpen(true)}
              className="inline-flex items-center gap-2 rounded-md bg-white border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
            >
              <Download className="h-4 w-4" />
              Import Existing Stacks
            </button>
            <Link
              to="/stacks/new"
              className="inline-flex items-center gap-2 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
            >
              <Plus className="h-4 w-4" />
              Create Stack
            </Link>
          </div>
        </div>
      ) : (
        <div className="space-y-4">
          {stacks.map((stack) => {
            const isRunning = stack.status === 'Running'
            const isStopped = stack.status === 'Stopped'
            const uptime = calculateStackUptime(stack)

            return (
              <div
                key={stack.stackId}
                className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm hover:shadow-md transition-shadow"
              >
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    <div className="flex items-center gap-3">
                      <Link
                        to={`/stacks/${stack.stackId}`}
                        className="text-xl font-semibold text-gray-900 hover:text-blue-600"
                      >
                        {stack.stackName}
                      </Link>
                      <span
                        className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${getStatusBadgeColor(stack.status)}`}
                      >
                        {stack.status}
                      </span>
                      {stack.updateStatus?.hasUpdates && (
                        <span className="flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-medium bg-amber-100 text-amber-800">
                          <AlertCircle className="h-3 w-3" />
                          Updates Available
                        </span>
                      )}
                      {uptime && (
                        <span className="text-xs text-green-600 font-medium">
                          Uptime: {uptime}
                        </span>
                      )}
                    </div>
                    <p className="mt-2 text-sm text-gray-500">
                      Created {new Date(stack.createdAt).toLocaleDateString()}
                    </p>
                    <div className="mt-3 flex gap-4 text-sm text-gray-600">
                      <span>Auth: {stack.configuration.ports.authServer}</span>
                      <span>World: {stack.configuration.ports.worldServer}</span>
                      <span>DB: {stack.configuration.database.port}</span>
                    </div>
                  </div>

                  <div className="flex gap-2">
                    {isStopped && (
                      <button
                        onClick={() => startStack.mutate(stack.stackId)}
                        disabled={startStack.isPending}
                        className="flex items-center gap-2 rounded-md bg-green-600 px-3 py-2 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50"
                      >
                        {startStack.isPending ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <Play className="h-4 w-4" />
                        )}
                        Start
                      </button>
                    )}
                    {isRunning && (
                      <>
                        <button
                          onClick={() => restartStack.mutate(stack.stackId)}
                          disabled={restartStack.isPending}
                          className="flex items-center gap-2 rounded-md bg-yellow-600 px-3 py-2 text-sm font-medium text-white hover:bg-yellow-700 disabled:opacity-50"
                        >
                          {restartStack.isPending ? (
                            <Loader2 className="h-4 w-4 animate-spin" />
                          ) : (
                            <RefreshCw className="h-4 w-4" />
                          )}
                          Restart
                        </button>
                        <button
                          onClick={() => stopStack.mutate(stack.stackId)}
                          disabled={stopStack.isPending}
                          className="flex items-center gap-2 rounded-md bg-red-600 px-3 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
                        >
                          {stopStack.isPending ? (
                            <Loader2 className="h-4 w-4 animate-spin" />
                          ) : (
                            <Square className="h-4 w-4" />
                          )}
                          Stop
                        </button>
                      </>
                    )}
                    {/* Rebuild button - available for Building/Failed states, or anytime really */}
                    {(stack.status === 'Building' || stack.status === 'Failed' || stack.status === 'Stopped') && (
                      <button
                        onClick={() => rebuildStack.mutate(stack.stackId)}
                        disabled={rebuildStack.isPending}
                        className="flex items-center gap-2 rounded-md bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
                        title={stack.status === 'Building' ? 'Retry build' : 'Rebuild stack'}
                      >
                        {rebuildStack.isPending ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <Hammer className="h-4 w-4" />
                        )}
                        Rebuild
                      </button>
                    )}
                    <button
                      onClick={() => setDeletingStack({ id: stack.stackId, name: stack.stackName })}
                      className="flex items-center gap-2 rounded-md border border-gray-300 bg-white px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
                      title="Delete stack"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                </div>
              </div>
            )
          })}
        </div>
      )}

      {/* Delete Confirmation Dialog */}
      {deletingStack && (
        <DeleteStackDialog
          stackName={deletingStack.name}
          onConfirm={() => deleteStack.mutate(deletingStack.id)}
          onCancel={() => setDeletingStack(null)}
          isDeleting={deleteStack.isPending}
        />
      )}
      
      {/* Import Stacks Dialog */}
      <ImportStacksDialog
        isOpen={importDialogOpen}
        onClose={() => setImportDialogOpen(false)}
        onImportSuccess={() => {
          queryClient.invalidateQueries({ queryKey: ['stacks'] })
        }}
      />
    </div>
  )
}

import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { CheckCircle, XCircle, Loader2, Play } from 'lucide-react'
import { useBuildProgress } from '@/hooks/useBuildProgress'
import { stackApi } from '@/services/api'

interface BuildProgressDialogProps {
  stackId: string
  stackName: string
  onClose: () => void
}

export default function BuildProgressDialog({ stackId, stackName, onClose }: BuildProgressDialogProps) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { progress, isComplete, error } = useBuildProgress(stackId)

  const startStack = useMutation({
    mutationFn: () => stackApi.start(stackId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['stacks'] })
      queryClient.invalidateQueries({ queryKey: ['stack', stackId] })
      navigate(`/stacks/${stackId}`)
    },
  })

  const isFailed = isComplete && error !== null
  const isSuccessful = isComplete && error === null
  const isBuilding = !isComplete

  useEffect(() => {
    if (isSuccessful) {
      const timer = setTimeout(() => {
        if (!startStack.isPending) {
          onClose()
        }
      }, 3000)
      return () => clearTimeout(timer)
    }
  }, [isSuccessful, startStack.isPending, onClose])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="w-full max-w-2xl rounded-lg bg-white shadow-xl">
        <div className="border-b border-gray-200 px-6 py-4">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-xl font-semibold text-gray-900">
                Building {stackName}
              </h2>
              <p className="mt-1 text-sm text-gray-500">
                {progress.step || progress.phase || 'Initializing...'}
              </p>
            </div>
            {isSuccessful && <CheckCircle className="h-8 w-8 text-green-500" />}
            {isFailed && <XCircle className="h-8 w-8 text-red-500" />}
            {isBuilding && <Loader2 className="h-8 w-8 animate-spin text-blue-500" />}
          </div>
        </div>

        {isBuilding && (
          <div className="px-6 py-4">
            <div className="h-2 w-full overflow-hidden rounded-full bg-gray-200">
              <div className="h-full bg-blue-500 transition-all duration-300" style={{ width: `${progress.percent}%` }} />
            </div>
            <p className="mt-2 text-sm text-gray-600">{progress.percent}%</p>
          </div>
        )}

        <div className="max-h-64 overflow-y-auto border-y border-gray-200 bg-gray-900 px-6 py-4 font-mono text-xs text-gray-100">
          {progress.logs.length === 0 && <p className="text-gray-400">Waiting for build to start...</p>}
          {progress.logs.map((log: string, index: number) => (
            <div key={index} className="whitespace-pre-wrap break-words">{log}</div>
          ))}
        </div>

        {isFailed && error && (
          <div className="border-t border-red-200 bg-red-50 px-6 py-4">
            <p className="text-sm font-medium text-red-800">Build Failed</p>
            <p className="mt-1 text-sm text-red-700">{error}</p>
          </div>
        )}

        {isSuccessful && (
          <div className="border-t border-green-200 bg-green-50 px-6 py-4">
            <p className="text-sm font-medium text-green-800">Build Completed Successfully!</p>
            <p className="mt-1 text-sm text-green-700">Your AzerothCore stack is ready to start.</p>
          </div>
        )}

        <div className="flex justify-end gap-3 px-6 py-4">
          {isFailed && (
            <button onClick={onClose} className="rounded-md bg-gray-100 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200">
              Close
            </button>
          )}
          {isSuccessful && (
            <>
              <button onClick={() => navigate(`/stacks/${stackId}`)} className="rounded-md bg-gray-100 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200">
                View Details
              </button>
              <button onClick={() => startStack.mutate()} disabled={startStack.isPending} className="flex items-center gap-2 rounded-md bg-green-600 px-4 py-2 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50">
                {startStack.isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
                    Starting...
                  </>
                ) : (
                  <>
                    <Play className="h-4 w-4" />
                    Start Stack
                  </>
                )}
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  )
}

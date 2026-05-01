import { AlertTriangle, Loader2 } from 'lucide-react'
import type { StackUpdateStatusDto } from '@/types/stack.types'

interface UpdateStackDialogProps {
  stackName: string
  updateStatus: StackUpdateStatusDto
  onConfirm: () => void
  onCancel: () => void
  isUpdating: boolean
}

export default function UpdateStackDialog({
  stackName,
  updateStatus,
  onConfirm,
  onCancel,
  isUpdating,
}: UpdateStackDialogProps) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50">
      <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
        <div className="mb-4 flex items-center gap-3">
          <AlertTriangle className="h-6 w-6 text-amber-600" />
          <h2 className="text-xl font-semibold text-gray-900">Update Stack</h2>
        </div>

        <div className="mb-6 space-y-4">
          <p className="text-gray-700">
            You are about to update <strong>{stackName}</strong> to the latest version.
          </p>

          <div className="rounded-md bg-amber-50 border border-amber-200 p-4">
            <h3 className="font-medium text-amber-900 mb-2">What will happen:</h3>
            <ul className="text-sm text-amber-800 space-y-1 list-disc list-inside">
              <li>The stack will be stopped if running</li>
              <li>Latest code will be pulled from GitHub</li>
              {updateStatus.isCoreOutdated && (
                <li>AzerothCore will be updated to latest commit</li>
              )}
              {updateStatus.outdatedModuleCount > 0 && (
                <li>
                  {updateStatus.outdatedModuleCount} module{updateStatus.outdatedModuleCount > 1 ? 's' : ''} will be updated
                </li>
              )}
              <li>The stack will be rebuilt (this may take 10-30 minutes)</li>
              <li>You can restart the stack after the build completes</li>
            </ul>
          </div>

          <div className="rounded-md bg-red-50 border border-red-200 p-4">
            <p className="text-sm text-red-800 font-medium">
              ⚠️ Warning: This will restart your server. Active players will be disconnected.
            </p>
          </div>
        </div>

        <div className="flex gap-3">
          <button
            onClick={onCancel}
            disabled={isUpdating}
            className="flex-1 rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            onClick={onConfirm}
            disabled={isUpdating}
            className="flex-1 flex items-center justify-center gap-2 rounded-md bg-amber-600 px-4 py-2 text-sm font-medium text-white hover:bg-amber-700 disabled:opacity-50"
          >
            {isUpdating ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" />
                Updating...
              </>
            ) : (
              'Update Stack'
            )}
          </button>
        </div>
      </div>
    </div>
  )
}

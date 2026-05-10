import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, BotMessageSquare, CheckCircle2, Loader2 } from 'lucide-react'
import { charactersApi, stackApi } from '@/services/api'
import { stackKeys } from '@/hooks/useStacks'
import type { StackDetailsDto } from '@/types/stack.types'

interface ModuleSetupWarningsProps {
  stack: StackDetailsDto
}

const AH_BOT_GUID_KEY = 'AC_AHBOT_GUIDS'

/**
 * Renders post-setup action warnings for modules that require additional configuration
 * after the stack is deployed. Each module can have its own inline action here.
 */
export default function ModuleSetupWarnings({ stack }: ModuleSetupWarningsProps) {
  const queryClient = useQueryClient()
  const [createDone, setCreateDone] = useState(false)

  const hasAhBot = stack.configuration.moduleIds?.includes('mod-ah-bot')
  const ahBotGuids = stack.configuration.advanced?.customEnvVars?.[AH_BOT_GUID_KEY]
  const ahBotNeedsSetup = hasAhBot && !ahBotGuids

  const createMutation = useMutation({
    mutationFn: async () => {
      const result = await charactersApi.createAhBotAccount(stack.stackId)
      const { allianceGuid, hordeGuid } = result.data
      const guids = [allianceGuid, hordeGuid].sort((a, b) => a - b).join(',')
      await stackApi.applyModuleConfig(stack.stackId, { [AH_BOT_GUID_KEY]: guids })
      return result.data
    },
    onSuccess: () => {
      setCreateDone(true)
      queryClient.invalidateQueries({ queryKey: stackKeys.detail(stack.stackId) })
    },
  })

  if (!ahBotNeedsSetup) return null

  return (
    <div className="mb-8 space-y-3">
      {/* AH Bot warning */}
      <div className="rounded-lg border border-amber-200 bg-amber-50 px-5 py-4">
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-start gap-3">
            <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-amber-600" />
            <div>
              <p className="text-sm font-semibold text-amber-900">Auction House Bot — setup required</p>
              <p className="mt-0.5 text-sm text-amber-800">
                The AH Bot module is installed but no bot characters have been created yet.
                Click the button to inject a dedicated <strong>AHBOT</strong> account with Alliance and Horde characters
                directly into the database. The stack needs to be restarted afterwards.
              </p>
            </div>
          </div>

          <div className="shrink-0">
            {createDone ? (
              <span className="flex items-center gap-1.5 text-sm font-medium text-green-700">
                <CheckCircle2 className="h-4 w-4" />
                Done — restart stack
              </span>
            ) : (
              <button
                onClick={() => createMutation.mutate()}
                disabled={createMutation.isPending}
                className="flex items-center gap-2 rounded-md bg-amber-600 px-4 py-2 text-sm font-medium text-white hover:bg-amber-700 disabled:cursor-not-allowed disabled:opacity-50 transition-colors"
              >
                {createMutation.isPending
                  ? <Loader2 className="h-4 w-4 animate-spin" />
                  : <BotMessageSquare className="h-4 w-4" />
                }
                Create AH Bot Characters
              </button>
            )}
          </div>
        </div>

        {createMutation.isError && (
          <p className="mt-2 text-sm text-red-700 pl-8">
            Failed to create characters — make sure the database container is running.
          </p>
        )}
      </div>
    </div>
  )
}

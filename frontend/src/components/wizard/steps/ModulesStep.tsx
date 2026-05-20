import { useState } from 'react'
import { AlertCircle, Check, ExternalLink, Loader2, Package } from 'lucide-react'
import type { WizardForm } from '@/components/wizard/types'
import { useModules } from '@/hooks/useModules'
import { cn } from '@/lib/utils'
import { ServerType } from '@/types/stack.types'

interface ModulesStepProps {
  form: WizardForm
}

export function ModulesStep({ form }: ModulesStepProps) {
  const { watch, setValue } = form
  const serverType = watch('serverType')
  const selectedIds = watch('moduleIds')
  const [search, setSearch] = useState('')

  const { data: modules, isLoading, isError } = useModules(serverType)

  const filtered = (modules ?? []).filter(
    (module) =>
      module.name.toLowerCase().includes(search.toLowerCase()) ||
      module.description.toLowerCase().includes(search.toLowerCase())
  )

  // Module-specific env var defaults to inject when a module is toggled on
  const MODULE_ENV_DEFAULTS: Record<string, Record<string, string>> = {
    'mod-ah-bot': { AC_AUCTION_HOUSE_BOT_GUIDS: '' },
  }

  const toggle = (id: string) => {
    const isRemoving = selectedIds.includes(id)
    const next = isRemoving
      ? selectedIds.filter((selectedId: string) => selectedId !== id)
      : [...selectedIds, id]

    setValue('moduleIds', next, { shouldDirty: true })

    // Auto-inject module-specific env var defaults when module is enabled
    if (!isRemoving && MODULE_ENV_DEFAULTS[id]) {
      const current = (form.getValues('advanced.customEnvVars') as Record<string, string>) ?? {}
      const merged = { ...MODULE_ENV_DEFAULTS[id], ...current } // current values win (don't overwrite existing)
      form.setValue('advanced.customEnvVars', merged, { shouldDirty: true })
    }
  }

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-xl font-semibold text-gray-900">Modules</h2>
        <p className="mt-1 text-sm text-gray-500">
          Select optional AzerothCore modules to include in your build.{` `}
          {serverType === ServerType.Playerbots && (
            <span className="font-medium text-amber-600">
              Some modules require the Playerbots variant.
            </span>
          )}
        </p>
      </div>

      <input
        type="search"
        placeholder="Search modules…"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
        className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        aria-label="Search modules"
      />

      {isLoading && (
        <div className="flex items-center justify-center gap-2 py-8 text-sm text-gray-500">
          <Loader2 className="h-5 w-5 animate-spin" aria-hidden="true" />
          Loading modules…
        </div>
      )}

      {isError && (
        <div className="flex items-center gap-2 rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
          <AlertCircle className="h-4 w-4 shrink-0" aria-hidden="true" />
          <span>
            Modules could not be loaded. You can continue — the module list will be available once the backend is running.
          </span>
        </div>
      )}

      {!isLoading && !isError && filtered.length === 0 && (
        <p className="py-6 text-center text-sm text-gray-400">
          {search ? 'No modules match your search.' : 'No modules available.'}
        </p>
      )}

      {!isLoading && !isError && filtered.length > 0 && (
        <ul className="grid gap-2" role="list" aria-label="Available modules">
          {filtered.map((module) => {
            const isSelected = selectedIds.includes(module.id)
            const incompatible = module.requiresPlayerbots && serverType !== ServerType.Playerbots

            return (
              <li key={module.id}>
                <button
                  type="button"
                  role="checkbox"
                  aria-checked={isSelected}
                  disabled={incompatible}
                  onClick={() => {
                    if (!incompatible) {
                      toggle(module.id)
                    }
                  }}
                  className={cn(
                    'flex w-full items-start gap-3 rounded-lg border-2 p-3 text-left transition-colors focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-1',
                    isSelected ? 'border-blue-600 bg-blue-50' : 'border-gray-200 bg-white hover:border-gray-300',
                    incompatible && 'cursor-not-allowed opacity-50'
                  )}
                >
                  <div
                    className={cn(
                      'mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded border-2',
                      isSelected ? 'border-blue-600 bg-blue-600' : 'border-gray-300 bg-white'
                    )}
                    aria-hidden="true"
                  >
                    {isSelected && <Check className="h-3 w-3 text-white" />}
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <Package className="h-3.5 w-3.5 shrink-0 text-gray-400" aria-hidden="true" />
                      <span className="text-sm font-medium text-gray-900">{module.name}</span>
                      {module.requiresPlayerbots && (
                        <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs text-amber-700">
                          Playerbots only
                        </span>
                      )}
                    </div>
                    <p className="mt-0.5 truncate text-xs text-gray-500">{module.description}</p>
                    {incompatible && (
                      <p className="mt-0.5 text-xs text-amber-600">
                        Switch to Playerbots server type to enable this module.
                      </p>
                    )}
                  </div>
                  {module.repository && (
                    <a
                      href={module.repository}
                      target="_blank"
                      rel="noopener noreferrer"
                      onClick={(event) => event.stopPropagation()}
                      className="mt-0.5 shrink-0 text-gray-400 hover:text-blue-600"
                      aria-label={`View ${module.name} repository`}
                    >
                      <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
                    </a>
                  )}
                </button>
              </li>
            )
          })}
        </ul>
      )}

      {selectedIds.length > 0 && (
        <p className="text-xs text-gray-500">
          {selectedIds.length} module{selectedIds.length !== 1 ? 's' : ''} selected
        </p>
      )}
    </div>
  )
}

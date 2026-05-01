import { useCallback, useMemo, useState } from 'react'
import { Plus, Trash2 } from 'lucide-react'
import { FormField } from '@/components/wizard/common/FormField'
import { ModuleConfigModal } from '@/components/wizard/ModuleConfigModal'
import type { WizardForm } from '@/components/wizard/types'
import { cn } from '@/lib/utils'

interface AdvancedStepProps {
  form: WizardForm
}

interface EnvPair {
  key: string
  value: string
}

function recordToArray(record: Record<string, string> | undefined): EnvPair[] {
  return Object.entries(record ?? {}).map(([key, value]) => ({ key, value }))
}

function arrayToRecord(pairs: EnvPair[]): Record<string, string> {
  return pairs.reduce<Record<string, string>>((accumulator, { key, value }) => {
    if (key.trim()) {
      accumulator[key.trim()] = value
    }

    return accumulator
  }, {})
}

export function AdvancedStep({ form }: AdvancedStepProps) {
  const {
    register,
    watch,
    setValue,
    formState: { errors },
  } = form
  const currentEnvVars = watch('advanced.customEnvVars')
  const selectedModules = (watch('moduleIds') ?? []) as string[]
  const pairs = useMemo(() => recordToArray(currentEnvVars), [currentEnvVars])
  const [showModuleConfig, setShowModuleConfig] = useState(false)

  // Build module name mapping
  const moduleNames: Record<string, string> = {
    'mod-autobalance': 'Auto Balance',
    'mod-playerbots': 'Playerbots',
    'mod-transmog': 'Transmogrification',
    'mod-ah-bot': 'Auction House Bot',
  }

  const syncToForm = useCallback((newPairs: EnvPair[]) => {
    setValue('advanced.customEnvVars', arrayToRecord(newPairs), { shouldDirty: true })
  }, [setValue])

  // Handle "Add" button click - open module config if modules exist, else add blank env var
  const handleAddClick = () => {
    if (selectedModules.length > 0) {
      setShowModuleConfig(true)
    } else {
      syncToForm([...pairs, { key: '', value: '' }])
    }
  }

  const removePair = (index: number) => {
    const next = pairs.filter((_, pairIndex) => pairIndex !== index)
    syncToForm(next)
  }

  const updatePair = (index: number, field: keyof EnvPair, value: string) => {
    const next = pairs.map((pair, pairIndex) => (
      pairIndex === index ? { ...pair, [field]: value } : pair
    ))
    syncToForm(next)
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold text-gray-900">Advanced Settings</h2>
        <p className="mt-1 text-sm text-gray-500">
          Fine-tune server behaviour. These can be changed after creation.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <FormField
          label="Max Players"
          htmlFor="max-players"
          error={errors.advanced?.maxPlayers?.message}
          hint="Concurrent player cap (1–10,000)"
          required
        >
          <input
            id="max-players"
            type="number"
            min={1}
            max={10000}
            className={cn(
              'block w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500',
              errors.advanced?.maxPlayers ? 'border-red-400' : 'border-gray-300'
            )}
            {...register('advanced.maxPlayers', { valueAsNumber: true })}
          />
        </FormField>

        <FormField
          label="Realm Name"
          htmlFor="realm-name"
          error={errors.advanced?.realmName?.message}
          hint="Displayed in the realm selection screen"
          required
        >
          <input
            id="realm-name"
            type="text"
            maxLength={64}
            className={cn(
              'block w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500',
              errors.advanced?.realmName ? 'border-red-400' : 'border-gray-300'
            )}
            {...register('advanced.realmName')}
          />
        </FormField>
      </div>

      <div>
        <div className="mb-2 flex items-center justify-between">
          <div>
            <span className="text-sm font-medium text-gray-700">Environment Variables</span>
            <p className="text-xs text-gray-500">
              {selectedModules.length > 0 
                ? 'Click "Add" to configure modules or add custom variables' 
                : 'Override AzerothCore configuration'}
            </p>
          </div>
          <button
            type="button"
            onClick={handleAddClick}
            className="inline-flex items-center gap-1.5 rounded-md border border-gray-300 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500"
            aria-label="Add environment variable"
          >
            <Plus className="h-3.5 w-3.5" aria-hidden="true" />
            Add
          </button>
        </div>

        {pairs.length === 0 && (
          <p className="rounded-md border border-dashed border-gray-200 py-4 text-center text-xs text-gray-400">
            No environment variables configured. Click "Add" to {selectedModules.length > 0 ? 'configure modules or add custom variables' : 'define variables'}.
          </p>
        )}

        {pairs.length > 0 && (
          <ul className="space-y-2" aria-label="Custom environment variables">
            {pairs.map((pair, index) => (
              <li key={index} className="flex items-center gap-2">
                <input
                  type="text"
                  placeholder="KEY"
                  value={pair.key}
                  onChange={(event) => updatePair(index, 'key', event.target.value)}
                  className="min-w-0 flex-1 rounded-md border border-gray-300 px-3 py-1.5 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                  aria-label={`Environment variable ${index + 1} key`}
                />
                <span className="text-sm text-gray-400" aria-hidden="true">=</span>
                <input
                  type="text"
                  placeholder="value"
                  value={pair.value}
                  onChange={(event) => updatePair(index, 'value', event.target.value)}
                  className="min-w-0 flex-1 rounded-md border border-gray-300 px-3 py-1.5 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                  aria-label={`Environment variable ${index + 1} value`}
                />
                <button
                  type="button"
                  onClick={() => removePair(index)}
                  className="shrink-0 rounded p-1 text-gray-400 hover:bg-red-50 hover:text-red-600 focus:outline-none focus:ring-2 focus:ring-red-500"
                  aria-label={`Remove environment variable ${index + 1}`}
                >
                  <Trash2 className="h-4 w-4" aria-hidden="true" />
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      {showModuleConfig && (
        <ModuleConfigModal
          selectedModuleIds={selectedModules}
          moduleNames={moduleNames}
          envVars={currentEnvVars ?? {}}
          onSave={(newEnvVars) => {
            setValue('advanced.customEnvVars', newEnvVars, { shouldDirty: true })
            setShowModuleConfig(false)
          }}
          onClose={() => setShowModuleConfig(false)}
        />
      )}
    </div>
  )
}

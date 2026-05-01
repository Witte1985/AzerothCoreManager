import type { ReactNode } from 'react'
import { AlertCircle, CheckCircle2 } from 'lucide-react'
import type { WizardForm } from '@/components/wizard/types'
import { ServerType, type PortFieldPath, type SuggestedPorts } from '@/types/stack.types'

interface ReviewStepProps {
  form: WizardForm
  validationErrors?: string[]
  isValidating?: boolean
  suggestedPorts?: SuggestedPorts
  onApplySuggestedPorts?: () => void
}

function ReviewRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex justify-between gap-4 border-b border-gray-100 py-2 last:border-0">
      <dt className="shrink-0 text-sm text-gray-500">{label}</dt>
      <dd className="text-right text-sm font-medium text-gray-900">{value}</dd>
    </div>
  )
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="overflow-hidden rounded-lg border border-gray-200">
      <div className="border-b border-gray-200 bg-gray-50 px-4 py-2.5">
        <h3 className="text-sm font-semibold text-gray-700">{title}</h3>
      </div>
      <dl className="px-4">{children}</dl>
    </div>
  )
}

const PORT_LABELS: Record<PortFieldPath, string> = {
  'database.port': 'MySQL',
  'ports.authServer': 'Auth',
  'ports.worldServer': 'World',
  'ports.soapPort': 'SOAP',
}

export function ReviewStep({
  form,
  validationErrors = [],
  isValidating,
  suggestedPorts = {},
  onApplySuggestedPorts,
}: ReviewStepProps) {
  const values = form.getValues()
  const customVarEntries = Object.entries(values.advanced.customEnvVars ?? {})
  const suggestedPortEntries = Object.entries(suggestedPorts) as Array<[PortFieldPath, number]>

  return (
    <div className="space-y-5">
      <div>
        <h2 className="text-xl font-semibold text-gray-900">Review &amp; Create</h2>
        <p className="mt-1 text-sm text-gray-500">
          Check your configuration before creating the stack.
        </p>
      </div>

      {isValidating && (
        <div className="flex items-center gap-2 py-2 text-sm text-gray-500">
          <span className="inline-block h-3 w-3 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" aria-hidden="true" />
          Validating configuration…
        </div>
      )}

      {validationErrors.length > 0 && (
        <div className="rounded-md border border-red-200 bg-red-50 p-4" role="alert" aria-label="Validation errors">
          <div className="mb-2 flex items-center gap-2">
            <AlertCircle className="h-4 w-4 shrink-0 text-red-500" aria-hidden="true" />
            <span className="text-sm font-medium text-red-700">Please fix these issues before creating:</span>
          </div>
          <ul className="list-inside list-disc space-y-1">
            {validationErrors.map((error, index) => (
              <li key={index} className="text-sm text-red-600">{error}</li>
            ))}
          </ul>
          {suggestedPortEntries.length > 0 && onApplySuggestedPorts && (
            <div className="mt-3 flex flex-wrap items-center gap-3">
              <button
                type="button"
                onClick={onApplySuggestedPorts}
                className="rounded-md border border-red-300 bg-white px-3 py-1.5 text-sm font-medium text-red-700 hover:bg-red-100 focus:outline-none focus:ring-2 focus:ring-red-500"
              >
                Use available ports
              </button>
              <span className="text-xs text-red-700">
                {suggestedPortEntries.map(([field, port]) => `${PORT_LABELS[field]} ${port}`).join(' · ')}
              </span>
            </div>
          )}
        </div>
      )}

      {validationErrors.length === 0 && !isValidating && (
        <div className="flex items-center gap-2 rounded-md border border-green-200 bg-green-50 px-4 py-2.5 text-sm text-green-700">
          <CheckCircle2 className="h-4 w-4 shrink-0" aria-hidden="true" />
          Configuration looks good!
        </div>
      )}

      <Section title="Server">
        <ReviewRow label="Stack Name" value={values.stackName || '—'} />
        <ReviewRow
          label="Server Type"
          value={
            <span className={values.serverType === ServerType.Playerbots ? 'text-amber-600' : undefined}>
              {values.serverType}
            </span>
          }
        />
      </Section>

      <Section title="Modules">
        <ReviewRow
          label="Selected Modules"
          value={
            values.moduleIds.length > 0
              ? `${values.moduleIds.length} module${values.moduleIds.length !== 1 ? 's' : ''}`
              : 'None'
          }
        />
      </Section>

      <Section title="Database">
        <ReviewRow label="Root Password" value="••••••••" />
        <ReviewRow label="MySQL Port" value={values.database.port} />
      </Section>

      <Section title="Ports">
        <ReviewRow label="Auth Server" value={values.ports.authServer} />
        <ReviewRow label="World Server" value={values.ports.worldServer} />
        <ReviewRow label="SOAP Port" value={values.ports.soapPort} />
      </Section>

      <Section title="Advanced">
        <ReviewRow label="Max Players" value={values.advanced.maxPlayers} />
        <ReviewRow label="Realm Name" value={values.advanced.realmName} />
        {customVarEntries.length > 0 && (
          <div className="border-b border-gray-100 py-2 last:border-0">
            <dt className="mb-2 text-sm text-gray-500">Custom Environment Variables</dt>
            <dd className="space-y-1">
              {customVarEntries.map(([key, value]) => (
                <div key={key} className="flex items-start gap-2 text-xs font-mono">
                  <span className="shrink-0 font-semibold text-gray-700">{key}</span>
                  <span className="text-gray-400">=</span>
                  <span className="break-all text-gray-600">{value}</span>
                </div>
              ))}
            </dd>
          </div>
        )}
      </Section>
    </div>
  )
}

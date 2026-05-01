import { Bot, Server } from 'lucide-react'
import { FormField } from '@/components/wizard/common/FormField'
import type { WizardForm } from '@/components/wizard/types'
import { cn } from '@/lib/utils'
import { ServerType } from '@/types/stack.types'

interface ServerConfigStepProps {
  form: WizardForm
}

export function ServerConfigStep({ form }: ServerConfigStepProps) {
  const {
    register,
    watch,
    setValue,
    formState: { errors },
  } = form

  const serverType = watch('serverType')

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold text-gray-900">Server Configuration</h2>
        <p className="mt-1 text-sm text-gray-500">
          Give your stack a name and choose the server variant.
        </p>
      </div>

      <FormField
        label="Stack Name"
        htmlFor="stackName"
        error={errors.stackName?.message}
        hint="Lowercase letters, numbers, and hyphens. E.g. my-wotlk-server"
        required
      >
        <input
          id="stackName"
          type="text"
          autoFocus
          autoComplete="off"
          placeholder="my-wotlk-server"
          className={cn(
            'block w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500',
            errors.stackName ? 'border-red-400' : 'border-gray-300'
          )}
          {...register('stackName')}
        />
      </FormField>

      <fieldset>
        <legend className="mb-2 text-sm font-medium text-gray-700">
          Server Type <span className="text-red-500" aria-hidden="true">*</span>
        </legend>
        {errors.serverType && (
          <p className="mb-2 text-xs text-red-600" role="alert">{errors.serverType.message}</p>
        )}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2" role="radiogroup" aria-label="Server type">
          {[
            {
              value: ServerType.Standard,
              icon: Server,
              title: 'Standard',
              description: 'Vanilla AzerothCore — the classic WotLK experience without bots.',
            },
            {
              value: ServerType.Playerbots,
              icon: Bot,
              title: 'Playerbots',
              description: 'Includes AI-controlled Playerbots so you can level and raid solo.',
            },
          ].map(({ value, icon: Icon, title, description }) => {
            const selected = serverType === value

            return (
              <button
                key={value}
                type="button"
                role="radio"
                aria-checked={selected}
                onClick={() => setValue('serverType', value, { shouldDirty: true, shouldValidate: true })}
                className={cn(
                  'flex items-start gap-3 rounded-lg border-2 p-4 text-left transition-colors focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2',
                  selected
                    ? 'border-blue-600 bg-blue-50'
                    : 'border-gray-200 bg-white hover:border-gray-300'
                )}
              >
                <Icon
                  className={cn('mt-0.5 h-5 w-5 shrink-0', selected ? 'text-blue-600' : 'text-gray-400')}
                  aria-hidden="true"
                />
                <div>
                  <div className={cn('font-medium', selected ? 'text-blue-700' : 'text-gray-900')}>
                    {title}
                  </div>
                  <div className="mt-0.5 text-xs text-gray-500">{description}</div>
                </div>
              </button>
            )
          })}
        </div>
      </fieldset>
    </div>
  )
}

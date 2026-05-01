import { z } from 'zod'
import { ServerType } from '@/types/stack.types'

export const serverConfigSchema = z.object({
  stackName: z
    .string()
    .min(2, 'Stack name must be at least 2 characters')
    .max(64, 'Stack name must be at most 64 characters')
    .regex(
      /^[a-z0-9]([a-z0-9-]*[a-z0-9])?$/,
      'Lowercase letters, numbers, hyphens only; cannot start or end with a hyphen'
    ),
  serverType: z.nativeEnum(ServerType),
})

export const modulesSchema = z.object({
  moduleIds: z.array(z.string()),
})

export const databaseSchema = z.object({
  database: z.object({
    rootPassword: z.string().min(8, 'Password must be at least 8 characters'),
    port: z.coerce
      .number()
      .int('Port must be a whole number')
      .min(1024, 'Port must be 1024 or higher')
      .max(65535, 'Port must be 65535 or lower'),
  }),
})

export const portsSchema = z.object({
  ports: z
    .object({
      authServer: z.coerce.number().int().min(1024).max(65535),
      worldServer: z.coerce.number().int().min(1024).max(65535),
      soapPort: z.coerce.number().int().min(1024).max(65535),
    })
    .refine(
      (ports) => new Set([ports.authServer, ports.worldServer, ports.soapPort]).size === 3,
      { message: 'All ports must be unique', path: ['authServer'] }
    ),
})

export const advancedSchema = z.object({
  advanced: z.object({
    maxPlayers: z.coerce
      .number()
      .int()
      .min(1, 'At least 1 player required')
      .max(10000, 'Maximum 10,000 players'),
    realmName: z.string().min(1, 'Realm name is required').max(64),
    customEnvVars: z.record(z.string(), z.string()).optional(),
  }),
})

export const wizardSchema = serverConfigSchema
  .merge(modulesSchema)
  .merge(databaseSchema)
  .merge(portsSchema)
  .merge(advancedSchema)

export type WizardFormData = z.infer<typeof wizardSchema>

export const WIZARD_DEFAULTS: WizardFormData = {
  stackName: '',
  serverType: ServerType.Standard,
  moduleIds: [],
  database: { rootPassword: '', port: 3306 },
  ports: { authServer: 3724, worldServer: 8085, soapPort: 7878 },
  advanced: { maxPlayers: 100, realmName: 'AzerothCore', customEnvVars: {} },
}

export const STEP_TRIGGER_FIELDS: Array<(keyof WizardFormData)[]> = [
  ['stackName', 'serverType'],
  ['moduleIds'],
  ['database'],
  ['ports'],
  ['advanced'],
  [],
]

import { useQuery } from '@tanstack/react-query'
import { moduleApi } from '@/services/api'
import type { ServerType } from '@/types/stack.types'

export function useModules(serverType?: ServerType) {
  return useQuery({
    queryKey: ['modules', serverType],
    queryFn: async () => {
      const response = await moduleApi.list(serverType)
      return response.data
    },
  })
}

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { stackApi } from '@/services/api'

export const stackKeys = {
  all: ['stacks'] as const,
  lists: () => [...stackKeys.all, 'list'] as const,
  list: (filters: string) => [...stackKeys.lists(), { filters }] as const,
  details: () => [...stackKeys.all, 'detail'] as const,
  detail: (id: string) => [...stackKeys.details(), id] as const,
}

export function useStacks() {
  return useQuery({
    queryKey: stackKeys.lists(),
    queryFn: async () => {
      const response = await stackApi.list()
      return response.data
    },
  })
}

export function useStack(stackId: string) {
  return useQuery({
    queryKey: stackKeys.detail(stackId),
    queryFn: async () => {
      const response = await stackApi.get(stackId)
      return response.data
    },
    enabled: !!stackId,
  })
}

export function useCreateStack() {
  const queryClient = useQueryClient()
  
  return useMutation({
    mutationFn: stackApi.create,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: stackKeys.lists() })
    },
  })
}

export function useDeleteStack() {
  const queryClient = useQueryClient()
  
  return useMutation({
    mutationFn: (stackId: string) => stackApi.delete(stackId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: stackKeys.lists() })
    },
  })
}

export function useStartStack() {
  const queryClient = useQueryClient()
  
  return useMutation({
    mutationFn: (stackId: string) => stackApi.start(stackId),
    onSuccess: (_, stackId) => {
      queryClient.invalidateQueries({ queryKey: stackKeys.detail(stackId) })
    },
  })
}

export function useStopStack() {
  const queryClient = useQueryClient()
  
  return useMutation({
    mutationFn: (stackId: string) => stackApi.stop(stackId),
    onSuccess: (_, stackId) => {
      queryClient.invalidateQueries({ queryKey: stackKeys.detail(stackId) })
    },
  })
}

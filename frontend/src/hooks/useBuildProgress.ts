import { useState, useEffect } from 'react'
import { useSignalR } from './useSignalR'
import { signalRService } from '@/services/signalr'
import { BuildPhase } from '@/types/stack.types'

interface BuildProgress {
  phase: BuildPhase
  percent: number
  step: string
  logs: string[]
}

export function useBuildProgress(stackId: string | null) {
  const [progress, setProgress] = useState<BuildProgress>({
    phase: BuildPhase.Cloning,
    percent: 0,
    step: '',
    logs: [],
  })
  const [isComplete, setIsComplete] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const { on, invoke } = useSignalR({
    hubUrl: '/hubs/buildprogress',
    autoConnect: !!stackId,
  })

  useEffect(() => {
    if (!stackId) return

    // Wait for connection before subscribing
    const subscribeWhenReady = async () => {
      try {
        // Ensure connection is established
        const maxWaitTime = 5000 // 5 seconds
        const startTime = Date.now()
        
        while (signalRService.state !== 'Connected' && Date.now() - startTime < maxWaitTime) {
          await new Promise(resolve => setTimeout(resolve, 100))
        }
        
        if (signalRService.state !== 'Connected') {
          console.error('SignalR connection timeout')
          return
        }

        // Subscribe to build
        await invoke('SubscribeToBuild', stackId)
        console.log(`Subscribed to build: ${stackId}`)
      } catch (err) {
        console.error('Failed to subscribe to build:', err)
      }
    }

    subscribeWhenReady()

    // Listen for events - backend sends multiple parameters, not objects
    const cleanupPhase = on(
      'BuildPhaseChanged',
      (receivedStackId: string, phase: BuildPhase) => {
        if (receivedStackId === stackId) {
          setProgress((prev) => ({ ...prev, phase }))
        }
      }
    )

    const cleanupProgress = on(
      'BuildProgressUpdated',
      (receivedStackId: string, percent: number, step: string) => {
        if (receivedStackId === stackId) {
          setProgress((prev) => ({
            ...prev,
            percent,
            step,
          }))
        }
      }
    )

    const cleanupLog = on(
      'BuildLogReceived',
      (receivedStackId: string, logLine: string) => {
        if (receivedStackId === stackId) {
          setProgress((prev) => ({
            ...prev,
            logs: [...prev.logs.slice(-50), logLine], // Keep last 50 lines
          }))
        }
      }
    )

    const cleanupComplete = on(
      'BuildCompleted',
      (receivedStackId: string) => {
        if (receivedStackId === stackId) {
          setIsComplete(true)
        }
      }
    )

    const cleanupFailed = on(
      'BuildFailed',
      (receivedStackId: string, errorMessage: string) => {
        if (receivedStackId === stackId) {
          setError(errorMessage)
          setIsComplete(true)
        }
      }
    )

    return () => {
      // Only unsubscribe if SignalR is connected
      if (signalRService.state === 'Connected') {
        invoke('UnsubscribeFromBuild', stackId).catch(console.error)
      }
      cleanupPhase()
      cleanupProgress()
      cleanupLog()
      cleanupComplete()
      cleanupFailed()
    }
  }, [stackId, on, invoke])

  return { progress, isComplete, error }
}

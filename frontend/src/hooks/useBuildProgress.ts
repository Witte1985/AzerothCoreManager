import { useState, useEffect, useRef } from 'react'
import { useSignalR } from './useSignalR'
import { signalRService } from '@/services/signalr'
import { buildApi } from '@/services/api'
import { BuildPhase } from '@/types/stack.types'

interface BuildProgress {
  phase: BuildPhase
  percent: number
  step: string
  logs: string[]
}

const POLL_INTERVAL_MS = 4000

export function useBuildProgress(stackId: string | null) {
  const [progress, setProgress] = useState<BuildProgress>({
    phase: BuildPhase.Cloning,
    percent: 0,
    step: '',
    logs: [],
  })
  const [isComplete, setIsComplete] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Track whether SignalR has delivered any update so polling can be skipped
  const signalRActiveRef = useRef(false)
  const isCompleteRef = useRef(false)

  const { on, invoke } = useSignalR({
    hubUrl: '/hubs/buildprogress',
    autoConnect: !!stackId,
  })

  // HTTP polling fallback: fetch build status periodically in case SignalR events are missed
  useEffect(() => {
    if (!stackId) return

    const poll = async () => {
      if (isCompleteRef.current) return
      try {
        const res = await buildApi.status(stackId)
        const status = res.data
        if (!signalRActiveRef.current) {
          // SignalR hasn't kicked in yet — seed state from HTTP
          setProgress({
            phase: status.currentPhase,
            percent: status.progressPercent,
            step: status.currentStep,
            logs: status.recentLogs ?? [],
          })
        }
        if (status.currentPhase === BuildPhase.Completed) {
          setIsComplete(true)
          isCompleteRef.current = true
        } else if (status.currentPhase === BuildPhase.Failed) {
          setError(status.errorMessage ?? 'Build failed')
          setIsComplete(true)
          isCompleteRef.current = true
        }
      } catch {
        // Polling failure is non-fatal; SignalR may still deliver events
      }
    }

    // Initial fetch immediately so page shows real state straight away
    poll()

    const intervalId = setInterval(poll, POLL_INTERVAL_MS)
    return () => clearInterval(intervalId)
  }, [stackId])

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
          signalRActiveRef.current = true
          setProgress((prev) => ({ ...prev, phase }))
        }
      }
    )

    const cleanupProgress = on(
      'BuildProgressUpdated',
      (receivedStackId: string, percent: number, step: string) => {
        if (receivedStackId === stackId) {
          signalRActiveRef.current = true
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
          signalRActiveRef.current = true
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
          signalRActiveRef.current = true
          isCompleteRef.current = true
          setIsComplete(true)
        }
      }
    )

    const cleanupFailed = on(
      'BuildFailed',
      (receivedStackId: string, errorMessage: string) => {
        if (receivedStackId === stackId) {
          signalRActiveRef.current = true
          isCompleteRef.current = true
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

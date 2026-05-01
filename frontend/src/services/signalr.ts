import * as signalR from '@microsoft/signalr'

class SignalRService {
  private connection: signalR.HubConnection | null = null
  private connectionPromise: Promise<void> | null = null

  async connect(hubUrl: string): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return
    }

    if (this.connectionPromise) {
      return this.connectionPromise
    }

    // In development, use full backend URL to avoid Vite proxy issues with WebSockets
    // In production, use relative URL (same origin)
    const fullUrl = import.meta.env.DEV 
      ? `http://localhost:5128${hubUrl}`
      : hubUrl

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(fullUrl)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 0s, 2s, 10s, 30s, then 30s
          if (retryContext.previousRetryCount === 0) return 0
          if (retryContext.previousRetryCount === 1) return 2000
          if (retryContext.previousRetryCount === 2) return 10000
          return 30000
        }
      })
      .configureLogging(signalR.LogLevel.Information)
      .build()

    // Setup reconnection handlers
    this.connection.onreconnecting((error) => {
      console.warn('SignalR reconnecting...', error)
    })

    this.connection.onreconnected((connectionId) => {
      console.log('SignalR reconnected:', connectionId)
    })

    this.connection.onclose((error) => {
      console.error('SignalR connection closed:', error)
      this.connectionPromise = null
    })

    this.connectionPromise = this.connection
      .start()
      .then(() => {
        console.log('SignalR connected to:', fullUrl)
        this.connectionPromise = null
      })
      .catch((err) => {
        console.error('SignalR connection failed:', err)
        this.connectionPromise = null
        throw err
      })

    return this.connectionPromise
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop()
      this.connection = null
      this.connectionPromise = null
    }
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- SignalR event callbacks can have varying signatures
  on(eventName: string, callback: (...args: any[]) => void): void {
    this.connection?.on(eventName, callback)
  }

  off(eventName: string): void {
    this.connection?.off(eventName)
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- SignalR invoke accepts dynamic parameters
  async invoke<T = any>(methodName: string, ...args: any[]): Promise<T> {
    if (!this.connection) {
      throw new Error('SignalR not initialized')
    }
    
    // If not connected, try to wait for connection
    if (this.connection.state !== signalR.HubConnectionState.Connected) {
      // If currently connecting, wait for it
      if (this.connectionPromise) {
        await this.connectionPromise
      } else {
        throw new Error('SignalR not connected')
      }
    }
    
    return this.connection.invoke<T>(methodName, ...args)
  }

  get state(): signalR.HubConnectionState | string | null {
    return this.connection?.state ?? null
  }
}

export const signalRService = new SignalRService()

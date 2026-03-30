import { useEffect, useState, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import type { HolidayAlert } from '../types'

const getNotifierUrl = () => {
  if (typeof window !== 'undefined') {
    const isDev = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1'
    if (isDev) return 'http://localhost:5002/hubs'
    return `${window.location.origin}/hubs`
  }
  return ''
}

export function useNotificationHub(
  baseUrl: string,
  accessToken: string,
  onHolidayDetected: (payload: HolidayAlert) => void
) {
  const [connected, setConnected] = useState(false)
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const callbackRef = useRef(onHolidayDetected)
  callbackRef.current = onHolidayDetected

  useEffect(() => {
    if (!accessToken) return

    const url = baseUrl || getNotifierUrl()
    console.log(`[SignalR] Connecting to ${url}/notifications`)

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${url}/notifications?access_token=${encodeURIComponent(accessToken)}`, {
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build()

    connection.on('HolidayDetected', (payload: HolidayAlert) => {
      console.log('[SignalR] Received HolidayDetected:', payload)
      callbackRef.current(payload)
    })

    connection
      .start()
      .then(() => {
        console.log('[SignalR] Connected successfully')
        setConnected(true)
      })
      .catch((err) => {
        console.error('[SignalR] Connection failed:', err)
        setConnected(false)
      })

    connection.onclose((error) => {
      console.log('[SignalR] Connection closed', error)
      setConnected(false)
    })
    connection.onreconnected(() => {
      console.log('[SignalR] Reconnected')
      setConnected(true)
    })

    connectionRef.current = connection
    return () => {
      console.log('[SignalR] Disconnecting...')
      connection.stop().catch(() => {})
      connectionRef.current = null
    }
  }, [baseUrl, accessToken])

  return { connected }
}

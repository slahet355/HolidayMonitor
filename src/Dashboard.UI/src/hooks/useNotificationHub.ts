import { useEffect, useState, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import type { HolidayAlert } from '../types'

const defaultUrl = typeof window !== 'undefined' ? `${window.location.origin}/hubs` : ''

export function useNotificationHub(
  baseUrl: string,
  userId: string,
  onHolidayDetected: (payload: HolidayAlert) => void
) {
  const [connected, setConnected] = useState(false)
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const callbackRef = useRef(onHolidayDetected)
  callbackRef.current = onHolidayDetected

  useEffect(() => {
    const url = baseUrl || defaultUrl
    console.log(`[SignalR] Connecting to ${url}/notifications with userId: ${userId}`)
    
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${url}/notifications?userId=${encodeURIComponent(userId)}`, { withCredentials: true })
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
        console.log(`[SignalR] Connected successfully with userId: ${userId}`)
        setConnected(true)
        connection.invoke('SetUserId', userId).catch((err) => {
          console.error('[SignalR] SetUserId failed:', err)
        })
      })
      .catch((err) => {
        console.error('[SignalR] Connection failed:', err)
        setConnected(false)
      })

    connection.onclose((error) => {
      console.log('[SignalR] Connection closed', error)
      setConnected(false)
    })
    connection.onreconnected((connectionId) => {
      console.log('[SignalR] Reconnected:', connectionId)
      setConnected(true)
      connection.invoke('SetUserId', userId).catch((err) => {
        console.error('[SignalR] SetUserId on reconnect failed:', err)
      })
    })

    connectionRef.current = connection
    return () => {
      console.log('[SignalR] Disconnecting...')
      connection.stop().catch(() => {})
      connectionRef.current = null
    }
  }, [baseUrl, userId])

  useEffect(() => {
    const conn = connectionRef.current
    if (conn && connected) {
      console.log(`[SignalR] Re-registering userId: ${userId}`)
      conn.invoke('SetUserId', userId).catch((err) => {
        console.error('[SignalR] SetUserId failed:', err)
      })
    }
  }, [userId, connected])

  return { connected }
}

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
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${url}/notifications`, { withCredentials: true })
      .withAutomaticReconnect()
      .build()

    connection.on('HolidayDetected', (payload: HolidayAlert) => {
      callbackRef.current(payload)
    })

    connection
      .start()
      .then(() => {
        setConnected(true)
        connection.invoke('SetUserId', userId).catch(() => {})
      })
      .catch(() => setConnected(false))

    connection.onclose(() => setConnected(false))
    connection.onreconnected(() => {
      setConnected(true)
      connection.invoke('SetUserId', userId).catch(() => {})
    })

    connectionRef.current = connection
    return () => {
      connection.stop().catch(() => {})
      connectionRef.current = null
    }
  }, [baseUrl, userId])

  useEffect(() => {
    const conn = connectionRef.current
    if (conn && connected) {
      conn.invoke('SetUserId', userId).catch(() => {})
    }
  }, [userId, connected])

  return { connected }
}

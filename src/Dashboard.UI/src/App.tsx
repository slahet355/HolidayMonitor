import { useState, useCallback } from 'react'
import { AuthProvider, useAuth } from './context/AuthContext'
import { AuthPage } from './components/AuthPage'
import { useNotificationHub } from './hooks/useNotificationHub'
import { useSubscriptions } from './hooks/useSubscriptions'
import { HolidayCard } from './components/HolidayCard'
import { SubscriptionPanel } from './components/SubscriptionPanel'
import type { HolidayAlert } from './types'

const API_BASE = import.meta.env.VITE_API_BASE ?? ''
const HUB_BASE = import.meta.env.VITE_HUB_BASE ?? ''

function Dashboard() {
  const { user, logout } = useAuth()
  const [alerts, setAlerts] = useState<HolidayAlert[]>([])

  const { countryCodes, setCountryCodes, loading: subsLoading } = useSubscriptions(
    API_BASE,
    user!.accessToken
  )
  const { connected } = useNotificationHub(HUB_BASE, user!.accessToken, (payload) => {
    setAlerts((prev) =>
      [{ ...payload, id: `${payload.detectedAtUtc}-${payload.countryCode}` }, ...prev].slice(0, 50)
    )
  })

  const handleCountriesChange = useCallback(
    (codes: string[]) => setCountryCodes(codes),
    [setCountryCodes]
  )

  return (
    <div className="min-h-screen bg-slate-950 text-slate-200">
      <header className="border-b border-slate-800 bg-slate-900/80 backdrop-blur">
        <div className="mx-auto max-w-6xl px-4 py-4 sm:px-6">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <h1 className="font-display text-2xl font-bold tracking-tight text-white sm:text-3xl">
              Holiday Monitor
            </h1>
            <div className="flex items-center gap-3">
              <span
                className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium ${
                  connected ? 'bg-emerald-500/20 text-emerald-400' : 'bg-amber-500/20 text-amber-400'
                }`}
              >
                <span
                  className={`h-1.5 w-1.5 rounded-full ${
                    connected ? 'bg-emerald-400' : 'bg-amber-400 animate-pulse'
                  }`}
                />
                {connected ? 'Live' : 'Connecting…'}
              </span>
              <span className="text-sm text-slate-400">{user!.email}</span>
              <button
                type="button"
                onClick={() => logout()}
                className="rounded-lg border border-slate-700 px-3 py-1.5 text-sm text-slate-400 hover:border-slate-600 hover:text-white"
              >
                Sign out
              </button>
            </div>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6">
        <div className="grid gap-8 lg:grid-cols-[1fr_320px]">
          <section>
            <h2 className="font-display mb-4 text-lg font-semibold text-white">Live holiday alerts</h2>
            {alerts.length === 0 ? (
              <div className="rounded-xl border border-slate-800 bg-slate-900/50 p-8 text-center text-slate-500">
                Alerts will appear here when a monitored country has a public holiday. Subscribe to countries below.
              </div>
            ) : (
              <ul className="space-y-3">
                {alerts.map((alert) => (
                  <HolidayCard key={alert.id} alert={alert} />
                ))}
              </ul>
            )}
          </section>
          <aside>
            <SubscriptionPanel
              countryCodes={countryCodes}
              loading={subsLoading}
              onCountriesChange={handleCountriesChange}
            />
          </aside>
        </div>
      </main>
    </div>
  )
}

function AppContent() {
  const { user, loading } = useAuth()

  if (loading) {
    return (
      <div className="min-h-screen bg-slate-950 flex items-center justify-center">
        <span className="text-slate-500 text-sm">Loading…</span>
      </div>
    )
  }

  return user ? <Dashboard /> : <AuthPage />
}

export default function App() {
  return (
    <AuthProvider>
      <AppContent />
    </AuthProvider>
  )
}

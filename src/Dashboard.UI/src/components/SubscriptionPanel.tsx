import { useState } from 'react'

const COMMON_COUNTRIES = [
  { code: 'US', name: 'United States' },
  { code: 'GB', name: 'United Kingdom' },
  { code: 'DE', name: 'Germany' },
  { code: 'FR', name: 'France' },
  { code: 'CA', name: 'Canada' },
  { code: 'AU', name: 'Australia' },
  { code: 'JP', name: 'Japan' },
  { code: 'IN', name: 'India' },
  { code: 'BR', name: 'Brazil' },
  { code: 'ES', name: 'Spain' },
  { code: 'IT', name: 'Italy' },
  { code: 'NL', name: 'Netherlands' },
]

interface Props {
  userId: string
  countryCodes: string[]
  loading: boolean
  onCountriesChange: (codes: string[]) => void
}

export function SubscriptionPanel({ userId, countryCodes, loading, onCountriesChange }: Props) {
  const [newCode, setNewCode] = useState('')

  const add = (code: string) => {
    const c = code.toUpperCase().trim()
    if (!c || countryCodes.includes(c)) return
    onCountriesChange([...countryCodes, c])
  }

  const remove = (code: string) => {
    onCountriesChange(countryCodes.filter((x) => x !== code))
  }

  return (
    <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-4">
      <h2 className="font-display mb-3 text-lg font-semibold text-white">Subscriptions</h2>
      <p className="mb-4 text-sm text-slate-500">
        Get alerts when these countries have a public holiday. Scraper runs hourly.
      </p>
      {loading ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : (
        <>
          <div className="mb-3 flex gap-2">
            <input
              type="text"
              value={newCode}
              onChange={(e) => setNewCode(e.target.value.toUpperCase())}
              onKeyDown={(e) => e.key === 'Enter' && add(newCode)}
              placeholder="e.g. US"
              maxLength={2}
              className="flex-1 rounded-lg border border-slate-700 bg-slate-800 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-emerald-500 focus:outline-none focus:ring-1 focus:ring-emerald-500"
            />
            <button
              type="button"
              onClick={() => add(newCode) || setNewCode('')}
              className="rounded-lg bg-emerald-600 px-3 py-2 text-sm font-medium text-white hover:bg-emerald-500"
            >
              Add
            </button>
          </div>
          <div className="mb-3 flex flex-wrap gap-2">
            {COMMON_COUNTRIES.map(({ code, name }) => (
              <button
                key={code}
                type="button"
                onClick={() => add(code)}
                className="rounded-md border border-slate-700 bg-slate-800 px-2 py-1 text-xs text-slate-300 hover:border-slate-600 hover:text-white"
                title={name}
              >
                {code}
              </button>
            ))}
          </div>
          <ul className="space-y-1">
            {countryCodes.length === 0 ? (
              <li className="text-sm text-slate-500">No countries subscribed.</li>
            ) : (
              countryCodes.map((code) => (
                <li key={code} className="flex items-center justify-between rounded-md bg-slate-800/80 px-2 py-1.5 text-sm">
                  <span className="font-medium text-slate-200">{code}</span>
                  <button
                    type="button"
                    onClick={() => remove(code)}
                    className="text-slate-500 hover:text-red-400"
                    aria-label={`Remove ${code}`}
                  >
                    ×
                  </button>
                </li>
              ))
            )}
          </ul>
        </>
      )}
    </div>
  )
}

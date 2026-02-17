import { useState, useEffect, useCallback } from 'react'

export function useSubscriptions(apiBase: string, userId: string) {
  const [countryCodes, setCountryCodesState] = useState<string[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!userId) {
      setCountryCodesState([])
      setLoading(false)
      return
    }
    const base = apiBase.replace(/\/$/, '')
    setLoading(true)
    fetch(`${base}/api/subscriptions/${encodeURIComponent(userId)}`)
      .then((r) => (r.ok ? r.json() : { countryCodes: [] }))
      .then((d) => setCountryCodesState(d.countryCodes ?? []))
      .catch(() => setCountryCodesState([]))
      .finally(() => setLoading(false))
  }, [apiBase, userId])

  const setCountryCodes = useCallback(
    (codes: string[]) => {
      if (!userId) return
      const base = apiBase.replace(/\/$/, '')
      setCountryCodesState(codes)
      fetch(`${base}/api/subscriptions/${encodeURIComponent(userId)}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId, countryCodes: codes }),
      }).catch(() => {})
    },
    [apiBase, userId]
  )

  return { countryCodes, setCountryCodes, loading }
}

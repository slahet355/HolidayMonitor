import { useState, useEffect, useCallback } from 'react'

export function useSubscriptions(apiBase: string, accessToken: string) {
  const [countryCodes, setCountryCodesState] = useState<string[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!accessToken) {
      setCountryCodesState([])
      setLoading(false)
      return
    }
    const base = apiBase.replace(/\/$/, '')
    setLoading(true)
    fetch(`${base}/api/subscriptions`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    })
      .then((r) => (r.ok ? r.json() : { countryCodes: [] }))
      .then((d) => setCountryCodesState(d.countryCodes ?? []))
      .catch(() => setCountryCodesState([]))
      .finally(() => setLoading(false))
  }, [apiBase, accessToken])

  const setCountryCodes = useCallback(
    (codes: string[]) => {
      if (!accessToken) return
      const base = apiBase.replace(/\/$/, '')
      setCountryCodesState(codes)
      fetch(`${base}/api/subscriptions`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${accessToken}`,
        },
        body: JSON.stringify({ countryCodes: codes }),
      }).catch(() => {})
    },
    [apiBase, accessToken]
  )

  return { countryCodes, setCountryCodes, loading }
}

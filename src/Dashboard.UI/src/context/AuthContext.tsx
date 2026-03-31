import { createContext, useContext, useEffect, useReducer, type ReactNode } from 'react'

const AUTH_BASE = import.meta.env.VITE_AUTH_BASE ?? 'http://localhost:5003'
const REFRESH_TOKEN_KEY = 'hm_refresh_token'

export interface AuthUser {
  userId: string
  email: string
  accessToken: string
}

interface AuthState {
  user: AuthUser | null
  loading: boolean
}

type AuthAction =
  | { type: 'LOADED'; user: AuthUser | null }
  | { type: 'LOGGED_IN'; user: AuthUser }
  | { type: 'LOGGED_OUT' }

function reducer(_state: AuthState, action: AuthAction): AuthState {
  switch (action.type) {
    case 'LOADED': return { user: action.user, loading: false }
    case 'LOGGED_IN': return { user: action.user, loading: false }
    case 'LOGGED_OUT': return { user: null, loading: false }
  }
}

interface AuthContextValue extends AuthState {
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

async function callAuth(endpoint: string, body: object) {
  const res = await fetch(`${AUTH_BASE}/auth/${endpoint}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  const data = await res.json()
  if (!res.ok) throw new Error(data.error ?? 'Authentication failed')
  return data as { accessToken: string; refreshToken: string; userId: string; email: string }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(reducer, { user: null, loading: true })

  // On mount: attempt silent refresh if we have a stored refresh token
  useEffect(() => {
    const storedRefresh = localStorage.getItem(REFRESH_TOKEN_KEY)
    if (!storedRefresh) {
      dispatch({ type: 'LOADED', user: null })
      return
    }
    callAuth('refresh', { refreshToken: storedRefresh })
      .then(({ accessToken, refreshToken, userId, email }) => {
        localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken)
        dispatch({ type: 'LOADED', user: { userId, email, accessToken } })
      })
      .catch(() => {
        localStorage.removeItem(REFRESH_TOKEN_KEY)
        dispatch({ type: 'LOADED', user: null })
      })
  }, [])

  const login = async (email: string, password: string) => {
    const { accessToken, refreshToken, userId, email: userEmail } = await callAuth('login', { email, password })
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken)
    dispatch({ type: 'LOGGED_IN', user: { userId, email: userEmail, accessToken } })
  }

  const register = async (email: string, password: string) => {
    const { accessToken, refreshToken, userId, email: userEmail } = await callAuth('register', { email, password })
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken)
    dispatch({ type: 'LOGGED_IN', user: { userId, email: userEmail, accessToken } })
  }

  const logout = async () => {
    const storedRefresh = localStorage.getItem(REFRESH_TOKEN_KEY)
    if (storedRefresh) {
      await callAuth('logout', { refreshToken: storedRefresh }).catch(() => {})
    }
    localStorage.removeItem(REFRESH_TOKEN_KEY)
    dispatch({ type: 'LOGGED_OUT' })
  }

  return (
    <AuthContext.Provider value={{ ...state, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}

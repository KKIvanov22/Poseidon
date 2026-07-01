import { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { logout as apiLogout } from '../api';

const SESSION_STORAGE_KEY = 'poseidon.auth';

const AuthContext = createContext(null);

function readStoredSession() {
  try {
    const stored = window.localStorage.getItem(SESSION_STORAGE_KEY);
    if (!stored) return null;

    const session = JSON.parse(stored);
    if (!session?.token || !session?.user) return null;

    if (session.expiresAt && new Date(session.expiresAt) <= new Date()) {
      window.localStorage.removeItem(SESSION_STORAGE_KEY);
      return null;
    }

    return session;
  } catch {
    window.localStorage.removeItem(SESSION_STORAGE_KEY);
    return null;
  }
}

function buildSession(auth) {
  return {
    token: auth.accessToken,
    expiresAt: auth.expiresAt,
    user: {
      displayName: auth.displayName,
      email: auth.email,
      role: auth.role,
      userId: auth.userId,
    },
  };
}

export function AuthProvider({ children }) {
  const [session, setSession] = useState(() => readStoredSession());

  const login = useCallback((auth) => {
    const nextSession = buildSession(auth);
    window.localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(nextSession));
    setSession(nextSession);
    return nextSession;
  }, []);

  const logout = useCallback(async () => {
    const token = session?.token;
    window.localStorage.removeItem(SESSION_STORAGE_KEY);
    setSession(null);

    if (token) {
      try {
        await apiLogout(token);
      } catch {
        // JWT logout is client-side; server acknowledgement is best-effort.
      }
    }
  }, [session?.token]);

  const value = useMemo(
    () => ({
      session,
      user: session?.user ?? null,
      token: session?.token ?? null,
      isAuthenticated: Boolean(session?.token && session?.user),
      login,
      logout,
    }),
    [session, login, logout]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}

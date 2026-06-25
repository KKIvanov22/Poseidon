import React, { useCallback, useState } from 'react';
import Login from './Login';
import SignUp from './SignUp';
import Dashboard from './Dashboard';
import { logout } from './api';

const SESSION_STORAGE_KEY = 'poseidon.auth';

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

function App() {
  const [session, setSession] = useState(() => readStoredSession()); // { token, user, expiresAt }
  const [view, setView] = useState(() => (session ? 'dashboard' : 'login')); // 'login' | 'signup' | 'dashboard'

  const handleAuthSuccess = (auth) => {
    const nextSession = {
      token: auth.accessToken,
      expiresAt: auth.expiresAt,
      user: {
        displayName: auth.displayName,
        email: auth.email,
        role: auth.role,
        userId: auth.userId,
      },
    };

    window.localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(nextSession));
    setSession(nextSession);
    setView('dashboard');
  };

  const handleLogout = useCallback(async () => {
    const token = session?.token;
    window.localStorage.removeItem(SESSION_STORAGE_KEY);
    setSession(null);
    setView('login');

    if (token) {
      try {
        await logout(token);
      } catch {
        // JWT logout is client-side; a failed server acknowledgement should not keep the user signed in.
      }
    }
  }, [session?.token]);

  if (view === 'dashboard' && session) {
    return <Dashboard user={session.user} token={session.token} onLogout={handleLogout} />;
  }

  return view === 'login' ? (
    <Login onSwitchToSignUp={() => setView('signup')} onLoginSuccess={handleAuthSuccess} />
  ) : (
    <SignUp onSwitchToLogin={() => setView('login')} onSignUpSuccess={handleAuthSuccess} />
  );
}

export default App;

import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { FiArrowRight, FiLock, FiMail, FiUser, FiUserPlus } from 'react-icons/fi';
import { register, ApiError } from './api';
import { useAuth } from './auth/AuthContext';
import { getDashboardPath } from './lib/roles';
import { useTheme } from './theme/ThemeContext';

export default function SignUp() {
  const navigate = useNavigate();
  const { login: saveSession } = useAuth();
  const { isDark } = useTheme();

  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError('');

    // basic client-side email validation
    const emailRe = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRe.test(email)) {
      setError('Please enter a valid email address.');
      return;
    }

    if (password !== confirmPassword) {
      setError("Passwords don't match.");
      return;
    }

    setSubmitting(true);
    try {
      const auth = await register(email, password, name);
      const session = saveSession(auth);
      navigate(getDashboardPath(session.user.role), { replace: true });
    } catch (err) {
      setError(
        err instanceof ApiError && err.status === 409
          ? 'An account with this email already exists.'
          : err.message || 'Something went wrong. Please try again.'
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className={`min-h-screen ${isDark ? 'bg-slate-950 text-slate-100' : 'bg-canvas text-slate-800'}`}>
      <header className={`border-b backdrop-blur ${isDark ? 'border-slate-800 bg-slate-900/80' : 'border-slate-200/70 bg-white/80'}`}>
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <div className="flex items-center gap-2">
            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-brand-500 font-display text-lg font-bold text-white">P</span>
            <span className={`font-display text-xl font-bold ${isDark ? 'text-slate-100' : 'text-ink'}`}>Poseidon</span>
          </div>
          <Link
            to="/login"
            className={`rounded-lg border px-3 py-2 text-sm font-semibold transition ${isDark ? 'border-slate-700 text-slate-200 hover:border-brand-400 hover:text-brand-400' : 'border-slate-200 text-slate-600 hover:border-brand-400 hover:text-brand-500'}`}
          >
            Log in
          </Link>
        </div>
      </header>

      <main className="mx-auto grid min-h-[calc(100vh-73px)] max-w-6xl items-center gap-10 px-6 py-10 lg:grid-cols-[1fr_460px]">
        <section className="hidden animate-fade-up lg:block">
          <p className="text-sm font-bold uppercase tracking-[0.18em] text-brand-500">Poseidon workspace</p>
          <h1 className={`mt-4 font-display text-5xl font-bold leading-tight ${isDark ? 'text-slate-100' : 'text-ink'}`}>
            Build a cleaner home for your event operations.
          </h1>
          <p className="mt-5 max-w-xl text-lg leading-8 text-slate-500">
            Create your account and step into the same focused dashboard used to organize event status, timing, locations, and capacity.
          </p>
          <div className="mt-8 grid max-w-xl grid-cols-3 gap-4">
            {['Status', 'Schedule', 'Capacity'].map((item) => (
              <div key={item} className={`rounded-2xl border p-4 shadow-card ${isDark ? 'border-slate-800 bg-slate-900' : 'border-slate-100 bg-white'}`}>
                <span className="block h-2 w-10 rounded-full bg-brand-500" />
                <p className={`mt-4 text-sm font-bold ${isDark ? 'text-slate-100' : 'text-ink'}`}>{item}</p>
                <p className={`mt-1 text-xs leading-5 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>Ready in dashboard</p>
              </div>
            ))}
          </div>
        </section>

        <section className={`animate-fade-up rounded-2xl border p-6 shadow-soft sm:p-8 ${isDark ? 'border-slate-800 bg-slate-900' : 'border-slate-100 bg-white'}`}>
          <div className="mb-8">
            <p className="mb-2 inline-flex items-center gap-2 rounded-full bg-brand-50 px-3 py-1 text-xs font-bold text-brand-600">
              <FiUserPlus /> New account
            </p>
            <h2 className={`font-display text-3xl font-bold ${isDark ? 'text-slate-100' : 'text-ink'}`}>Sign up</h2>
            <p className={`mt-2 text-sm leading-6 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
              Create your account to start managing events in Poseidon.
            </p>
          </div>

          <form className="space-y-4" onSubmit={handleSubmit} noValidate>
            <div>
              <label htmlFor="name" className={`mb-2 flex items-center gap-2 text-sm font-bold ${isDark ? 'text-slate-200' : 'text-ink'}`}>
                <FiUser className="text-brand-500" /> Full name
              </label>
              <input
                id="name"
                type="text"
                autoComplete="name"
                placeholder="Your full name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                className={`w-full rounded-lg border px-3 py-3 text-sm outline-none transition placeholder:text-slate-400 focus:border-brand-400 focus:ring-4 focus:ring-brand-100 ${isDark ? 'border-slate-700 bg-slate-800 text-slate-100 focus:bg-slate-700' : 'border-slate-200 bg-slate-50 text-ink focus:bg-white'}`}
                required
              />
            </div>

            <div>
              <label htmlFor="email" className={`mb-2 flex items-center gap-2 text-sm font-bold ${isDark ? 'text-slate-200' : 'text-ink'}`}>
                <FiMail className="text-brand-500" /> Email
              </label>
              <input
                id="email"
                type="email"
                autoComplete="email"
                placeholder="you@example.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className={`w-full rounded-lg border px-3 py-3 text-sm outline-none transition placeholder:text-slate-400 focus:border-brand-400 focus:ring-4 focus:ring-brand-100 ${isDark ? 'border-slate-700 bg-slate-800 text-slate-100 focus:bg-slate-700' : 'border-slate-200 bg-slate-50 text-ink focus:bg-white'}`}
                required
              />
            </div>

            <div>
              <label htmlFor="password" className={`mb-2 flex items-center gap-2 text-sm font-bold ${isDark ? 'text-slate-200' : 'text-ink'}`}>
                <FiLock className="text-brand-500" /> Password
              </label>
              <input
                id="password"
                type="password"
                autoComplete="new-password"
                placeholder="Create a password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className={`w-full rounded-lg border px-3 py-3 text-sm outline-none transition placeholder:text-slate-400 focus:border-brand-400 focus:ring-4 focus:ring-brand-100 ${isDark ? 'border-slate-700 bg-slate-800 text-slate-100 focus:bg-slate-700' : 'border-slate-200 bg-slate-50 text-ink focus:bg-white'}`}
                required
              />
            </div>

            <div>
              <label htmlFor="confirmPassword" className={`mb-2 flex items-center gap-2 text-sm font-bold ${isDark ? 'text-slate-200' : 'text-ink'}`}>
                <FiLock className="text-brand-500" /> Confirm password
              </label>
              <input
                id="confirmPassword"
                type="password"
                autoComplete="new-password"
                placeholder="Repeat your password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                className={`w-full rounded-lg border px-3 py-3 text-sm outline-none transition placeholder:text-slate-400 focus:border-brand-400 focus:ring-4 focus:ring-brand-100 ${isDark ? 'border-slate-700 bg-slate-800 text-slate-100 focus:bg-slate-700' : 'border-slate-200 bg-slate-50 text-ink focus:bg-white'}`}
                required
              />
            </div>

            {error && (
              <p
                role="alert"
                className={`rounded-lg px-3 py-2 text-sm font-semibold ${isDark ? 'border-accent-600 bg-slate-800 text-accent-200' : 'border-accent-100 bg-accent-50 text-accent-600'}`}
              >
                {error}
              </p>
            )}

            <button
              type="submit"
              disabled={submitting}
              className="flex w-full items-center justify-center gap-2 rounded-lg bg-brand-500 px-4 py-3 text-sm font-bold text-white shadow-card transition hover:bg-brand-600 disabled:cursor-not-allowed disabled:opacity-70"
            >
              {submitting ? 'Creating account...' : 'Sign up'}
              {!submitting && <FiArrowRight />}
            </button>
          </form>

          <p className={`mt-6 text-center text-sm ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
            Already have an account?{' '}
            <Link to="/login" className="font-bold text-brand-600 transition hover:text-brand-700">
              Log in
            </Link>
          </p>
        </section>
      </main>
    </div>
  );
}

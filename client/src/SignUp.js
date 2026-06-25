import { useState } from 'react';
import { FiArrowRight, FiLock, FiMail, FiUser, FiUserPlus } from 'react-icons/fi';
import { register, ApiError } from './api';


const SignUp = ({ onSwitchToLogin, onSignUpSuccess }) => {
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError('');

    if (password !== confirmPassword) {
      setError("Passwords don't match.");
      return;
    }

    setSubmitting(true);
    try {
      const auth = await register(email, password, name);
      onSignUpSuccess(auth);
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
    <div className="min-h-screen bg-canvas">
      <header className="border-b border-slate-200/70 bg-white/80 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <div className="flex items-center gap-2">
            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-brand-500 font-display text-lg font-bold text-white">P</span>
            <span className="font-display text-xl font-bold text-ink">Poseidon</span>
          </div>
          <button
            type="button"
            onClick={onSwitchToLogin}
            className="rounded-lg border border-slate-200 px-3 py-2 text-sm font-semibold text-slate-600 transition hover:border-brand-400 hover:text-brand-500"
          >
            Log in
          </button>
        </div>
      </header>

      <main className="mx-auto grid min-h-[calc(100vh-73px)] max-w-6xl items-center gap-10 px-6 py-10 lg:grid-cols-[1fr_460px]">
        <section className="hidden animate-fade-up lg:block">
          <p className="text-sm font-bold uppercase tracking-[0.18em] text-brand-500">Poseidon workspace</p>
          <h1 className="mt-4 font-display text-5xl font-bold leading-tight text-ink">
            Build a cleaner home for your event operations.
          </h1>
          <p className="mt-5 max-w-xl text-lg leading-8 text-slate-500">
            Create your account and step into the same focused dashboard used to organize event status, timing, locations, and capacity.
          </p>
          <div className="mt-8 grid max-w-xl grid-cols-3 gap-4">
            {['Status', 'Schedule', 'Capacity'].map((item) => (
              <div key={item} className="rounded-2xl border border-slate-100 bg-white p-4 shadow-card">
                <span className="block h-2 w-10 rounded-full bg-brand-500" />
                <p className="mt-4 text-sm font-bold text-ink">{item}</p>
                <p className="mt-1 text-xs leading-5 text-slate-500">Ready in dashboard</p>
              </div>
            ))}
          </div>
        </section>

        <section className="animate-fade-up rounded-2xl border border-slate-100 bg-white p-6 shadow-soft sm:p-8">
          <div className="mb-8">
            <p className="mb-2 inline-flex items-center gap-2 rounded-full bg-brand-50 px-3 py-1 text-xs font-bold text-brand-600">
              <FiUserPlus /> New account
            </p>
            <h2 className="font-display text-3xl font-bold text-ink">Sign up</h2>
            <p className="mt-2 text-sm leading-6 text-slate-500">
              Create your account to start managing events in Poseidon.
            </p>
          </div>

          <form className="space-y-4" onSubmit={handleSubmit}>
            <div>
              <label htmlFor="name" className="mb-2 flex items-center gap-2 text-sm font-bold text-ink">
                <FiUser className="text-brand-500" /> Full name
              </label>
              <input
                id="name"
                type="text"
                placeholder="Your full name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                className="w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-3 text-sm text-ink outline-none transition placeholder:text-slate-400 focus:border-brand-400 focus:bg-white focus:ring-4 focus:ring-brand-100"
                required
              />
            </div>

            <div>
              <label htmlFor="email" className="mb-2 flex items-center gap-2 text-sm font-bold text-ink">
                <FiMail className="text-brand-500" /> Email
              </label>
              <input
                id="email"
                type="email"
                placeholder="you@example.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-3 text-sm text-ink outline-none transition placeholder:text-slate-400 focus:border-brand-400 focus:bg-white focus:ring-4 focus:ring-brand-100"
                required
              />
            </div>

            <div>
              <label htmlFor="password" className="mb-2 flex items-center gap-2 text-sm font-bold text-ink">
                <FiLock className="text-brand-500" /> Password
              </label>
              <input
                id="password"
                type="password"
                placeholder="Create a password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-3 text-sm text-ink outline-none transition placeholder:text-slate-400 focus:border-brand-400 focus:bg-white focus:ring-4 focus:ring-brand-100"
                required
              />
            </div>

            <div>
              <label htmlFor="confirmPassword" className="mb-2 flex items-center gap-2 text-sm font-bold text-ink">
                <FiLock className="text-brand-500" /> Confirm password
              </label>
              <input
                id="confirmPassword"
                type="password"
                placeholder="Repeat your password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                className="w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-3 text-sm text-ink outline-none transition placeholder:text-slate-400 focus:border-brand-400 focus:bg-white focus:ring-4 focus:ring-brand-100"
                required
              />
            </div>

            {error && (
              <p className="rounded-lg border border-accent-100 bg-accent-50 px-3 py-2 text-sm font-semibold text-accent-600">
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

          <p className="mt-6 text-center text-sm text-slate-500">
            Already have an account?{' '}
            <button type="button" className="font-bold text-brand-600 transition hover:text-brand-700" onClick={onSwitchToLogin}>
              Log in
            </button>
          </p>
        </section>
      </main>
    </div>
  );
}
 
export default SignUp;

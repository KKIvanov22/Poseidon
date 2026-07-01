import { useMemo, useState } from 'react';
import { FiMoon, FiSun, FiLock, FiCheckCircle, FiAlertCircle, FiHome } from 'react-icons/fi';
import { useAuth } from '../auth/AuthContext';
import { useApiError } from '../auth/useApiError';
import { changePassword } from '../api';
import DashboardLayout from '../components/DashboardLayout';
import { useTheme } from '../theme/ThemeContext';
import { Link } from 'react-router-dom';

const INITIAL_FORM = {
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
};

export default function UserProfilePage() {
  const { user, token } = useAuth();
  const { theme, isDark, toggleTheme } = useTheme();
  const handleApiError = useApiError();

  const [form, setForm] = useState(INITIAL_FORM);
  const [status, setStatus] = useState('idle');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  const firstName = useMemo(() => user?.displayName?.split(' ')[0] || '', [user?.displayName]);

  const handleChange = (event) => {
    const { name, value } = event.target;
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setStatus('submitting');
    setError('');
    setMessage('');

    if (!form.currentPassword || !form.newPassword || !form.confirmPassword) {
      setError('Please fill in all password fields.');
      setStatus('idle');
      return;
    }

    if (form.newPassword.length < 8) {
      setError('New password must be at least 8 characters long.');
      setStatus('idle');
      return;
    }

    if (form.newPassword !== form.confirmPassword) {
      setError('New password and confirmation do not match.');
      setStatus('idle');
      return;
    }

    try {
      await changePassword(token, {
        currentPassword: form.currentPassword,
        newPassword: form.newPassword,
      });
      setMessage('Password updated successfully.');
      setForm(INITIAL_FORM);
    } catch (err) {
      if (handleApiError(err)) return;
      setError(err.message || 'Unable to update your password right now.');
    } finally {
      setStatus('idle');
    }
  };

  return (
    <DashboardLayout
      badge="Your profile"
      title={`Profile${firstName ? ` — ${firstName}` : ''}`}
      subtitle="Update your account password and personalize your experience."
    >
      <div className="mb-6 flex justify-start">
        <Link
          to="/"
          className={`inline-flex items-center gap-2 rounded-xl border px-3.5 py-2 text-sm font-semibold transition ${isDark ? 'border-slate-700 bg-slate-900 text-slate-200 hover:border-brand-400 hover:text-brand-400' : 'border-slate-200 bg-white text-ink hover:border-brand-400 hover:text-brand-600'}`}
        >
          <FiHome /> Back to home
        </Link>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1.2fr_0.8fr]">
        <section className={`rounded-3xl border p-6 shadow-card ${isDark ? 'border-slate-800 bg-slate-900' : 'border-slate-200 bg-white'}`}>
          <div className="mb-5 flex items-center gap-3">
            <div className={`flex h-12 w-12 items-center justify-center rounded-2xl ${isDark ? 'bg-brand-500/15 text-brand-300' : 'bg-brand-50 text-brand-600'}`}>
              <FiLock className="text-xl" />
            </div>
            <div>
              <h2 className={`text-xl font-bold ${isDark ? 'text-slate-100' : 'text-ink'}`}>Security settings</h2>
              <p className={`text-sm ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>Keep your account secure with a fresh password.</p>
            </div>
          </div>

          <form onSubmit={handleSubmit} className="space-y-4">
            <label className="block">
              <span className={`mb-1.5 block text-sm font-semibold ${isDark ? 'text-slate-200' : 'text-ink'}`}>Current password</span>
              <input
                type="password"
                name="currentPassword"
                value={form.currentPassword}
                onChange={handleChange}
                autoComplete="current-password"
                className={`w-full rounded-xl border px-3 py-2.5 text-sm outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100 ${isDark ? 'border-slate-700 bg-slate-800 text-slate-100 placeholder:text-slate-400 focus:bg-slate-700' : 'border-slate-200 bg-slate-50 text-ink focus:bg-white'}`}
              />
            </label>

            <label className="block">
              <span className={`mb-1.5 block text-sm font-semibold ${isDark ? 'text-slate-200' : 'text-ink'}`}>New password</span>
              <input
                type="password"
                name="newPassword"
                value={form.newPassword}
                onChange={handleChange}
                autoComplete="new-password"
                className={`w-full rounded-xl border px-3 py-2.5 text-sm outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100 ${isDark ? 'border-slate-700 bg-slate-800 text-slate-100 placeholder:text-slate-400 focus:bg-slate-700' : 'border-slate-200 bg-slate-50 text-ink focus:bg-white'}`}
              />
            </label>

            <label className="block">
              <span className={`mb-1.5 block text-sm font-semibold ${isDark ? 'text-slate-200' : 'text-ink'}`}>Confirm new password</span>
              <input
                type="password"
                name="confirmPassword"
                value={form.confirmPassword}
                onChange={handleChange}
                autoComplete="new-password"
                className={`w-full rounded-xl border px-3 py-2.5 text-sm outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100 ${isDark ? 'border-slate-700 bg-slate-800 text-slate-100 placeholder:text-slate-400 focus:bg-slate-700' : 'border-slate-200 bg-slate-50 text-ink focus:bg-white'}`}
              />
            </label>

            {message && (
              <div className="flex items-start gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-2.5 text-sm text-emerald-700">
                <FiCheckCircle className="mt-0.5 shrink-0" />
                <span>{message}</span>
              </div>
            )}

            {error && (
              <div className="flex items-start gap-2 rounded-xl border border-accent-200 bg-accent-50 px-3 py-2.5 text-sm text-accent-700">
                <FiAlertCircle className="mt-0.5 shrink-0" />
                <span>{error}</span>
              </div>
            )}

            <button
              type="submit"
              disabled={status === 'submitting'}
              className="inline-flex items-center justify-center rounded-xl bg-brand-500 px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-brand-600 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {status === 'submitting' ? 'Updating...' : 'Change password'}
            </button>
          </form>
        </section>

        <section className={`rounded-3xl border p-6 shadow-card ${isDark ? 'border-slate-800 bg-slate-900' : 'border-slate-200 bg-white'}`}>
          <div className="mb-5 flex items-center gap-3">
            <div className={`flex h-12 w-12 items-center justify-center rounded-2xl ${isDark ? 'bg-slate-800 text-slate-300' : 'bg-slate-100 text-slate-600'}`}>
              {theme === 'dark' ? <FiMoon className="text-xl" /> : <FiSun className="text-xl" />}
            </div>
            <div>
              <h2 className={`text-xl font-bold ${isDark ? 'text-slate-100' : 'text-ink'}`}>Appearance</h2>
              <p className={`text-sm ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>Switch between your default theme and dark mode.</p>
            </div>
          </div>

          <div className={`rounded-2xl border p-4 ${isDark ? 'border-slate-800 bg-slate-800/70' : 'border-slate-200 bg-slate-50'}`}>
            <p className={`text-sm font-semibold ${isDark ? 'text-slate-100' : 'text-ink'}`}>Current theme</p>
            <p className={`mt-1 text-sm ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
              {theme === 'dark' ? 'Dark mode is active.' : 'Default theme is active.'}
            </p>
            <button
              type="button"
              onClick={toggleTheme}
              className={`mt-4 inline-flex items-center gap-2 rounded-xl border px-3.5 py-2 text-sm font-semibold transition ${isDark ? 'border-slate-700 bg-slate-900 text-slate-200 hover:border-brand-400 hover:text-brand-400' : 'border-slate-200 bg-white text-ink hover:border-brand-400 hover:text-brand-600'}`}
            >
              {theme === 'dark' ? <FiSun /> : <FiMoon />}
              {theme === 'dark' ? 'Switch to default' : 'Switch to dark'}
            </button>
          </div>

          <div className={`mt-6 rounded-2xl border p-4 ${isDark ? 'border-slate-800 bg-slate-800/70' : 'border-slate-200 bg-slate-50'}`}>
            <p className={`text-sm font-semibold ${isDark ? 'text-slate-100' : 'text-ink'}`}>Account summary</p>
            <ul className={`mt-3 space-y-2 text-sm ${isDark ? 'text-slate-300' : 'text-slate-600'}`}>
              <li><span className={`font-semibold ${isDark ? 'text-slate-100' : 'text-ink'}`}>Name:</span> {user?.displayName || '—'}</li>
              <li><span className={`font-semibold ${isDark ? 'text-slate-100' : 'text-ink'}`}>Email:</span> {user?.email || '—'}</li>
              <li><span className={`font-semibold ${isDark ? 'text-slate-100' : 'text-ink'}`}>Role:</span> {user?.role || '—'}</li>
            </ul>
          </div>
        </section>
      </div>
    </DashboardLayout>
  );
}

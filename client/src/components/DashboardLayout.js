import { FiLogOut, FiUser } from 'react-icons/fi';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { useTheme } from '../theme/ThemeContext';

export default function DashboardLayout({ badge, title, subtitle, children }) {
  const { user, logout } = useAuth();
  const { isDark } = useTheme();

  return (
    <div className={`min-h-screen ${isDark ? 'bg-slate-950 text-slate-100' : 'bg-canvas text-slate-800'}`}>
      <header className={`sticky top-0 z-10 border-b backdrop-blur ${isDark ? 'border-slate-800 bg-slate-900/80' : 'border-slate-200/70 bg-white/80'}`}>
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <div className="flex items-center gap-2">
            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-brand-500 font-display text-lg font-bold text-white">
              P
            </span>
            <span className={`font-display text-xl font-bold ${isDark ? 'text-slate-100' : 'text-ink'}`}>Poseidon</span>
          </div>
          <div className="flex items-center gap-3">
            <div className="hidden text-right sm:block">
              <p className={`text-sm font-semibold ${isDark ? 'text-slate-100' : 'text-ink'}`}>{user?.displayName || 'Welcome'}</p>
              <p className={`text-xs ${isDark ? 'text-slate-400' : 'text-slate-400'}`}>{user?.email}</p>
            </div>
            <Link
              to="/profile"
              className={`flex items-center gap-2 rounded-lg border px-3 py-2 text-sm font-medium transition ${isDark ? 'border-slate-700 text-slate-200 hover:border-brand-400 hover:text-brand-400' : 'border-slate-200 text-slate-600 hover:border-brand-400 hover:text-brand-600'}`}
            >
              <FiUser /> Profile
            </Link>
            <button
              type="button"
              onClick={logout}
              className={`flex items-center gap-2 rounded-lg border px-3 py-2 text-sm font-medium transition ${isDark ? 'border-slate-700 text-slate-200 hover:border-accent-400 hover:text-accent-400' : 'border-slate-200 text-slate-600 hover:border-accent-500 hover:text-accent-600'}`}
            >
              <FiLogOut /> Log out
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-6 py-8">
        <div className="mb-8 flex flex-wrap items-start justify-between gap-4">
          <div>
            <h1 className={`font-display text-2xl font-bold sm:text-3xl ${isDark ? 'text-slate-100' : 'text-ink'}`}>
              {title}
            </h1>
            {subtitle && <p className={`mt-1 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>{subtitle}</p>}
          </div>
          {badge && (
            <span className={`rounded-full border px-3 py-1 text-sm font-semibold ${isDark ? 'border-brand-500/40 bg-brand-500/10 text-brand-300' : 'border-brand-100 bg-brand-50 text-brand-700'}`}>
              {badge}
            </span>
          )}
        </div>
        {children}
      </main>
    </div>
  );
}

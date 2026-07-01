import { FiLogOut } from 'react-icons/fi';
import { useAuth } from '../auth/AuthContext';

export default function DashboardLayout({ badge, title, subtitle, children }) {
  const { user, logout } = useAuth();

  return (
    <div className="min-h-screen bg-canvas">
      <header className="sticky top-0 z-10 border-b border-slate-200/70 bg-white/80 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <div className="flex items-center gap-2">
            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-brand-500 font-display text-lg font-bold text-white">
              P
            </span>
            <span className="font-display text-xl font-bold text-ink">Poseidon</span>
          </div>
          <div className="flex items-center gap-4">
            <div className="hidden text-right sm:block">
              <p className="text-sm font-semibold text-ink">{user?.displayName || 'Welcome'}</p>
              <p className="text-xs text-slate-400">{user?.email}</p>
            </div>
            <button
              type="button"
              onClick={logout}
              className="flex items-center gap-2 rounded-lg border border-slate-200 px-3 py-2 text-sm font-medium text-slate-600 transition hover:border-accent-500 hover:text-accent-600"
            >
              <FiLogOut /> Log out
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-6 py-8">
        <div className="mb-8 flex flex-wrap items-start justify-between gap-4">
          <div>
            <h1 className="font-display text-2xl font-bold text-ink sm:text-3xl">
              {title}
            </h1>
            {subtitle && <p className="mt-1 text-slate-500">{subtitle}</p>}
          </div>
          {badge && (
            <span className="rounded-full border border-brand-100 bg-brand-50 px-3 py-1 text-sm font-semibold text-brand-700">
              {badge}
            </span>
          )}
        </div>
        {children}
      </main>
    </div>
  );
}

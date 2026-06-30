import { useEffect, useMemo, useState, useCallback } from 'react';
import {
  FiCalendar,
  FiMapPin,
  FiUsers,
  FiSearch,
  FiLogOut,
  FiRefreshCw,
  FiAlertCircle,
  FiInbox,
} from 'react-icons/fi';
import { getMyEvents, ApiError } from './api';

const STATUS_META = {
  1: { label: 'Draft', dot: 'bg-amber-400', pill: 'bg-amber-50 text-amber-700 ring-amber-200' },
  2: { label: 'Published', dot: 'bg-emerald-500', pill: 'bg-emerald-50 text-emerald-700 ring-emerald-200' },
  3: { label: 'Cancelled', dot: 'bg-accent', pill: 'bg-accent-50 text-accent-600 ring-accent-100' },
  4: { label: 'Completed', dot: 'bg-slate-400', pill: 'bg-slate-100 text-slate-600 ring-slate-200' },
};
const FALLBACK_STATUS = { label: 'Unknown', dot: 'bg-slate-400', pill: 'bg-slate-100 text-slate-600 ring-slate-200' };

const FILTERS = [
  { key: 'all', label: 'All' },
  { key: 2, label: 'Published' },
  { key: 1, label: 'Draft' },
  { key: 3, label: 'Cancelled' },
  { key: 4, label: 'Completed' },
];

function relativeLabel(date, now) {
  const units = [
    ['year', 365 * 24 * 60 * 60 * 1000],
    ['month', 30 * 24 * 60 * 60 * 1000],
    ['day', 24 * 60 * 60 * 1000],
    ['hour', 60 * 60 * 1000],
    ['minute', 60 * 1000],
  ];
  const diffMs = date - now;
  const abs = Math.abs(diffMs);
  for (const [unit, unitMs] of units) {
    if (abs >= unitMs || unit === 'minute') {
      const value = Math.round(diffMs / unitMs);
      return new Intl.RelativeTimeFormat('en', { numeric: 'auto' }).format(value, unit);
    }
  }
  return 'now';
}

function getTimeContext(startsAt, endsAt, statusId, now) {
  if (statusId === 3) return { label: 'Cancelled', tone: 'muted' };
  const start = new Date(startsAt);
  const end = new Date(endsAt);
  if (now < start) return { label: `Starts ${relativeLabel(start, now)}`, tone: 'upcoming' };
  if (now <= end) return { label: 'Live now', tone: 'live' };
  return { label: `Ended ${relativeLabel(end, now)}`, tone: 'ended' };
}

function formatDateRange(startsAt, endsAt) {
  const start = new Date(startsAt);
  const end = new Date(endsAt);
  const dateFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' });
  const timeFmt = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' });
  const sameDay = start.toDateString() === end.toDateString();
  return sameDay
    ? `${dateFmt.format(start)} · ${timeFmt.format(start)} – ${timeFmt.format(end)}`
    : `${dateFmt.format(start)} ${timeFmt.format(start)} – ${dateFmt.format(end)} ${timeFmt.format(end)}`;
}

function StatCard({ label, value, accent }) {
  return (
    <div className="rounded-2xl border border-slate-100 bg-white p-5 shadow-card">
      <p className="text-sm font-medium text-slate-500">{label}</p>
      <p className={`mt-1 font-display text-3xl font-bold tabular-nums ${accent}`}>{value}</p>
    </div>
  );
}

function TimeBadge({ tone, label }) {
  const toneClasses = {
    live: 'bg-accent-50 text-accent-600',
    upcoming: 'bg-brand-50 text-brand-600',
    ended: 'bg-slate-100 text-slate-500',
    muted: 'bg-slate-100 text-slate-400',
  };
  return (
    <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ${toneClasses[tone]}`}>
      {tone === 'live' && (
        <span className="relative flex h-2 w-2">
          <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-accent opacity-75" />
          <span className="relative inline-flex h-2 w-2 rounded-full bg-accent" />
        </span>
      )}
      {label}
    </span>
  );
}

function EventCard({ event, now, index }) {
  const status = STATUS_META[event.eventStatusId] || FALLBACK_STATUS;
  const time = getTimeContext(event.startsAt, event.endsAt, event.eventStatusId, now);

  return (
    <div
      className="group flex flex-col gap-4 rounded-2xl border border-slate-100 bg-white p-6 shadow-card transition hover:-translate-y-0.5 hover:shadow-card-hover animate-fade-up"
      style={{ animationDelay: `${Math.min(index, 8) * 40}ms` }}
    >
      <div className="flex items-start justify-between gap-3">
        <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ring-1 ring-inset ${status.pill}`}>
          <span className={`h-1.5 w-1.5 rounded-full ${status.dot}`} />
          {status.label}
        </span>
        <TimeBadge tone={time.tone} label={time.label} />
      </div>

      <div>
        <h3 className="font-display text-lg font-bold leading-snug text-ink line-clamp-2">{event.title}</h3>
        <p className="mt-1 line-clamp-2 text-sm text-slate-500">
          {event.description || <span className="italic text-slate-400">No description provided.</span>}
        </p>
      </div>

      <div className="mt-auto space-y-2 border-t border-slate-100 pt-4 text-sm text-slate-600">
        <div className="flex items-center gap-2">
          <FiCalendar className="shrink-0 text-brand-500" />
          <span>{formatDateRange(event.startsAt, event.endsAt)}</span>
        </div>
        <div className="flex items-center gap-2">
          <FiMapPin className="shrink-0 text-brand-500" />
          <span className="truncate">{event.locationText || 'Location to be announced'}</span>
        </div>
        <div className="flex items-center gap-2">
          <FiUsers className="shrink-0 text-brand-500" />
          <span>{event.capacity} {event.capacity === 1 ? 'seat' : 'seats'} capacity</span>
        </div>
      </div>
    </div>
  );
}

function CardSkeleton() {
  return (
    <div className="animate-pulse rounded-2xl border border-slate-100 bg-white p-6 shadow-card">
      <div className="flex justify-between">
        <div className="h-5 w-20 rounded-full bg-slate-100" />
        <div className="h-5 w-24 rounded-full bg-slate-100" />
      </div>
      <div className="mt-4 h-5 w-3/4 rounded bg-slate-100" />
      <div className="mt-2 h-4 w-full rounded bg-slate-100" />
      <div className="mt-1 h-4 w-2/3 rounded bg-slate-100" />
      <div className="mt-6 space-y-2 border-t border-slate-100 pt-4">
        <div className="h-3.5 w-1/2 rounded bg-slate-100" />
        <div className="h-3.5 w-1/3 rounded bg-slate-100" />
        <div className="h-3.5 w-2/5 rounded bg-slate-100" />
      </div>
    </div>
  );
}

const Dashboard = ({ user, token, onLogout }) => {
  const [events, setEvents] = useState([]);
  const [status, setStatus] = useState('loading'); // loading | ready | error | forbidden
  const [errorMessage, setErrorMessage] = useState('');
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('all');
  const [now, setNow] = useState(new Date());

  const canAccessDashboard = ['teacher', 'admin'].includes((user?.role || '').toLowerCase());

  const loadEvents = useCallback(async () => {
    if (!canAccessDashboard) {
      setStatus('forbidden');
      setEvents([]);
      return;
    }

    setStatus('loading');
    try {
      const data = await getMyEvents(token);
      setEvents(data);
      setStatus('ready');
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        onLogout();
        return;
      }
      setErrorMessage(err.message || 'Something went wrong while loading events.');
      setStatus('error');
    }
  }, [canAccessDashboard, token, onLogout]);

  useEffect(() => {
    loadEvents();
  }, [loadEvents]);

  // Keep "Live now" / relative timestamps fresh without re-fetching.
  useEffect(() => {
    const interval = setInterval(() => setNow(new Date()), 60000);
    return () => clearInterval(interval);
  }, []);

  const stats = useMemo(() => {
    const tally = { total: events.length, 1: 0, 2: 0, 3: 0, 4: 0 };
    events.forEach((e) => {
      if (tally[e.eventStatusId] !== undefined) tally[e.eventStatusId] += 1;
    });
    return tally;
  }, [events]);

  const filteredEvents = useMemo(() => {
    return events
      .filter((e) => (filter === 'all' ? true : e.eventStatusId === filter))
      .filter((e) => {
        const q = search.trim().toLowerCase();
        if (!q) return true;
        return (
          e.title.toLowerCase().includes(q) ||
          (e.locationText || '').toLowerCase().includes(q)
        );
      });
  }, [events, filter, search]);

  if (!canAccessDashboard) {
    return (
      <div className="min-h-screen bg-canvas">
        <header className="sticky top-0 z-10 border-b border-slate-200/70 bg-white/80 backdrop-blur">
          <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
            <div className="flex items-center gap-2">
              <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-brand-500 font-display text-lg font-bold text-white">P</span>
              <span className="font-display text-xl font-bold text-ink">Poseidon</span>
            </div>
            <button
              type="button"
              onClick={onLogout}
              className="flex items-center gap-2 rounded-lg border border-slate-200 px-3 py-2 text-sm font-medium text-slate-600 transition hover:border-accent-500 hover:text-accent-600"
            >
              <FiLogOut /> Log out
            </button>
          </div>
        </header>

        <main className="mx-auto flex max-w-4xl items-center justify-center px-6 py-16">
          <div className="w-full rounded-3xl border border-slate-200 bg-white p-8 text-center shadow-card">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-accent-50 text-accent-600">
              <FiAlertCircle className="text-2xl" />
            </div>
            <h1 className="mt-6 font-display text-2xl font-bold text-ink">Access restricted</h1>
            <p className="mt-3 text-slate-500">
              This teacher dashboard is available only for Teacher and Admin accounts.
            </p>
            <p className="mt-2 text-sm text-slate-400">
              Sign in with a teacher account to view the events you created.
            </p>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-canvas">
      <header className="sticky top-0 z-10 border-b border-slate-200/70 bg-white/80 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <div className="flex items-center gap-2">
            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-brand-500 font-display text-lg font-bold text-white">P</span>
            <span className="font-display text-xl font-bold text-ink">Poseidon</span>
          </div>
          <div className="flex items-center gap-4">
            <div className="hidden text-right sm:block">
              <p className="text-sm font-semibold text-ink">{user?.displayName || 'Welcome'}</p>
              <p className="text-xs text-slate-400">{user?.email}</p>
            </div>
            <button
              type="button"
              onClick={onLogout}
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
              Welcome back{user?.displayName ? `, ${user.displayName.split(' ')[0]}` : ''}
            </h1>
            <p className="mt-1 text-slate-500">Here are the events you created and manage as a teacher.</p>
          </div>
          <span className="rounded-full border border-brand-100 bg-brand-50 px-3 py-1 text-sm font-semibold text-brand-700">
            Teacher dashboard
          </span>
        </div>

        <div className="mb-8 grid grid-cols-2 gap-4 sm:grid-cols-4">
          <StatCard label="Total events" value={stats.total} accent="text-ink" />
          <StatCard label="Published" value={stats[2]} accent="text-emerald-600" />
          <StatCard label="Draft" value={stats[1]} accent="text-amber-600" />
          <StatCard label="Cancelled" value={stats[3]} accent="text-accent-600" />
        </div>

        <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex flex-wrap gap-2">
            {FILTERS.map((f) => (
              <button
                key={f.key}
                type="button"
                onClick={() => setFilter(f.key)}
                className={`rounded-full px-3.5 py-1.5 text-sm font-medium transition ${
                  filter === f.key
                    ? 'bg-brand-500 text-white shadow-sm'
                    : 'bg-white text-slate-600 ring-1 ring-inset ring-slate-200 hover:bg-slate-50'
                }`}
              >
                {f.label}
              </button>
            ))}
          </div>

          <div className="flex items-center gap-2">
            <div className="relative">
              <FiSearch className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search title or location"
                className="w-full rounded-lg border border-slate-200 bg-white py-2 pl-9 pr-3 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100 sm:w-64"
              />
            </div>
            <button
              type="button"
              onClick={loadEvents}
              title="Refresh"
              className="flex items-center justify-center rounded-lg border border-slate-200 p-2 text-slate-500 transition hover:border-brand-400 hover:text-brand-500"
            >
              <FiRefreshCw className={status === 'loading' ? 'animate-spin' : ''} />
            </button>
          </div>
        </div>

        {status === 'error' && (
          <div className="flex flex-col items-center gap-3 rounded-2xl border border-accent-100 bg-accent-50 px-6 py-14 text-center">
            <FiAlertCircle className="text-3xl text-accent-600" />
            <p className="font-semibold text-ink">Couldn't load events</p>
            <p className="max-w-sm text-sm text-slate-500">{errorMessage}</p>
            <button
              type="button"
              onClick={loadEvents}
              className="mt-2 rounded-lg bg-brand-500 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-600"
            >
              Try again
            </button>
          </div>
        )}

        {status === 'loading' && (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
          </div>
        )}

        {status === 'ready' && filteredEvents.length === 0 && (
          <div className="flex flex-col items-center gap-3 rounded-2xl border border-slate-100 bg-white px-6 py-14 text-center">
            <FiInbox className="text-3xl text-slate-300" />
            <p className="font-semibold text-ink">
              {events.length === 0 ? 'No events created yet' : 'No events match your filters'}
            </p>
            <p className="max-w-sm text-sm text-slate-500">
              {events.length === 0
                ? 'Once you create an event, it will appear here for your teacher dashboard.'
                : 'Try a different search term or clear the status filter.'}
            </p>
          </div>
        )}

        {status === 'ready' && filteredEvents.length > 0 && (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {filteredEvents.map((event, i) => (
              <EventCard key={event.eventId} event={event} now={now} index={i} />
            ))}
          </div>
        )}
      </main>
    </div>
  );
};

export default Dashboard;

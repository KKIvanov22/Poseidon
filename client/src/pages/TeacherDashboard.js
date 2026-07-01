import { useCallback, useEffect, useMemo, useState } from 'react';
import { FiAlertCircle, FiInbox, FiRefreshCw, FiSearch } from 'react-icons/fi';
import { getMyEvents } from '../api';
import { useAuth } from '../auth/AuthContext';
import { useApiError } from '../auth/useApiError';
import DashboardLayout from '../components/DashboardLayout';
import CardSkeleton from '../components/events/CardSkeleton';
import EventCard from '../components/events/EventCard';
import StatCard from '../components/events/StatCard';
import { TEACHER_FILTERS } from '../lib/eventUtils';

export default function TeacherDashboard() {
  const { user, token } = useAuth();
  const handleApiError = useApiError();

  const [events, setEvents] = useState([]);
  const [status, setStatus] = useState('loading');
  const [errorMessage, setErrorMessage] = useState('');
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('all');
  const [now, setNow] = useState(new Date());

  const loadEvents = useCallback(async () => {
    setStatus('loading');
    try {
      const data = await getMyEvents(token);
      setEvents(data);
      setStatus('ready');
    } catch (err) {
      if (handleApiError(err)) return;
      setErrorMessage(err.message || 'Something went wrong while loading events.');
      setStatus('error');
    }
  }, [token, handleApiError]);

  useEffect(() => {
    loadEvents();
  }, [loadEvents]);

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

  const firstName = user?.displayName?.split(' ')[0];

  return (
    <DashboardLayout
      badge="Teacher dashboard"
      title={`Welcome back${firstName ? `, ${firstName}` : ''}`}
      subtitle="Here are the events you created and manage as a teacher."
    >
      <div className="mb-8 grid grid-cols-2 gap-4 sm:grid-cols-4">
        <StatCard label="Total events" value={stats.total} accent="text-ink" />
        <StatCard label="Published" value={stats[2]} accent="text-emerald-600" />
        <StatCard label="Draft" value={stats[1]} accent="text-amber-600" />
        <StatCard label="Cancelled" value={stats[3]} accent="text-accent-600" />
      </div>

      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-wrap gap-2">
          {TEACHER_FILTERS.map((f) => (
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
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search title or location"
              aria-label="Search events"
              className="w-full rounded-lg border border-slate-200 bg-white py-2 pl-9 pr-3 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100 sm:w-64"
            />
          </div>
          <button
            type="button"
            onClick={loadEvents}
            title="Refresh"
            aria-label="Refresh events"
            className="flex items-center justify-center rounded-lg border border-slate-200 p-2 text-slate-500 transition hover:border-brand-400 hover:text-brand-500"
          >
            <FiRefreshCw className={status === 'loading' ? 'animate-spin' : ''} />
          </button>
        </div>
      </div>

      {status === 'error' && (
        <div className="flex flex-col items-center gap-3 rounded-2xl border border-accent-100 bg-accent-50 px-6 py-14 text-center">
          <FiAlertCircle className="text-3xl text-accent-600" />
          <p className="font-semibold text-ink">Couldn&apos;t load events</p>
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
          {Array.from({ length: 6 }).map((_, i) => (
            <CardSkeleton key={i} />
          ))}
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
    </DashboardLayout>
  );
}

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
  FiPlus,
  FiX,
  FiCheck,
  FiCheckCircle,
  FiRotateCcw,
  FiBell,
  FiSend,
} from 'react-icons/fi';
import {
  getEvents,
  createEvent,
  publishEvent,
  cancelEvent,
  registerForEvent,
  getPendingNotificationJobs,
  completeNotificationJob,
  retryNotificationJob,
  ApiError,
} from './api';

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

function ActionButton({ tone = 'brand', icon, label, onClick, disabled, busy }) {
  const toneClasses = {
    brand: 'bg-brand-500 text-white hover:bg-brand-600',
    accent: 'bg-accent-50 text-accent-600 ring-1 ring-inset ring-accent-100 hover:bg-accent-100',
    ghost: 'bg-white text-slate-600 ring-1 ring-inset ring-slate-200 hover:bg-slate-50',
  };
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled || busy}
      className={`inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-semibold transition disabled:cursor-not-allowed disabled:opacity-60 ${toneClasses[tone]}`}
    >
      {busy ? <FiRefreshCw className="animate-spin" /> : icon}
      {label}
    </button>
  );
}

function EventCard({ event, now, index, user, busyEventId, onPublish, onCancel, onRegister }) {
  const status = STATUS_META[event.eventStatusId] || FALLBACK_STATUS;
  const time = getTimeContext(event.startsAt, event.endsAt, event.eventStatusId, now);
  const busy = busyEventId === event.eventId;

  const canManage = user?.role === 'Admin' || (user?.role === 'Teacher' && event.organizerId === user?.userId);
  const canRegister = user?.role === 'Student' && event.eventStatusId === 2;

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

      <div className="space-y-2 border-t border-slate-100 pt-4 text-sm text-slate-600">
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

      {(canManage || canRegister) && (
        <div className="mt-auto flex flex-wrap gap-2 border-t border-slate-100 pt-4">
          {canManage && event.eventStatusId === 1 && (
            <ActionButton tone="brand" icon={<FiSend />} label="Publish" busy={busy} onClick={() => onPublish(event)} />
          )}
          {canManage && (event.eventStatusId === 1 || event.eventStatusId === 2) && (
            <ActionButton tone="accent" icon={<FiX />} label="Cancel" busy={busy} onClick={() => onCancel(event)} />
          )}
          {canRegister && (
            <ActionButton tone="ghost" icon={<FiCheck />} label="Register" busy={busy} onClick={() => onRegister(event)} />
          )}
        </div>
      )}
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

function NewEventForm({ onCreate, onClose }) {
  const [form, setForm] = useState({
    title: '',
    description: '',
    locationText: '',
    startsAt: '',
    endsAt: '',
    capacity: 20,
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  const update = (field) => (e) => setForm((f) => ({ ...f, [field]: e.target.value }));

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    if (!form.title.trim() || !form.startsAt || !form.endsAt) {
      setError('Title, start time, and end time are required.');
      return;
    }
    setSubmitting(true);
    try {
      await onCreate({
        title: form.title.trim(),
        description: form.description.trim() || null,
        locationText: form.locationText.trim() || null,
        startsAt: new Date(form.startsAt).toISOString(),
        endsAt: new Date(form.endsAt).toISOString(),
        capacity: Number(form.capacity) || 0,
      });
    } catch (err) {
      setError(err.message || 'Could not create the event.');
      setSubmitting(false);
    }
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="mb-8 rounded-2xl border border-slate-100 bg-white p-6 shadow-card animate-fade-up"
    >
      <div className="mb-4 flex items-center justify-between">
        <h2 className="font-display text-lg font-bold text-ink">New draft event</h2>
        <button type="button" onClick={onClose} className="text-slate-400 transition hover:text-slate-600">
          <FiX />
        </button>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <label className="sm:col-span-2 text-sm font-medium text-slate-600">
          Title
          <input
            type="text"
            value={form.title}
            onChange={update('title')}
            placeholder="Fall robotics showcase"
            className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
          />
        </label>

        <label className="sm:col-span-2 text-sm font-medium text-slate-600">
          Description
          <textarea
            value={form.description}
            onChange={update('description')}
            placeholder="What should attendees know?"
            rows={2}
            className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
          />
        </label>

        <label className="text-sm font-medium text-slate-600">
          Starts at
          <input
            type="datetime-local"
            value={form.startsAt}
            onChange={update('startsAt')}
            className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
          />
        </label>

        <label className="text-sm font-medium text-slate-600">
          Ends at
          <input
            type="datetime-local"
            value={form.endsAt}
            onChange={update('endsAt')}
            className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
          />
        </label>

        <label className="text-sm font-medium text-slate-600">
          Location
          <input
            type="text"
            value={form.locationText}
            onChange={update('locationText')}
            placeholder="Main hall"
            className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
          />
        </label>

        <label className="text-sm font-medium text-slate-600">
          Capacity
          <input
            type="number"
            min={1}
            value={form.capacity}
            onChange={update('capacity')}
            className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
          />
        </label>
      </div>

      {error && <p className="mt-4 text-sm font-medium text-accent-600">{error}</p>}

      <div className="mt-5 flex items-center gap-3">
        <button
          type="submit"
          disabled={submitting}
          className="inline-flex items-center gap-2 rounded-lg bg-brand-500 px-4 py-2 text-sm font-bold text-white transition hover:bg-brand-600 disabled:cursor-not-allowed disabled:opacity-70"
        >
          {submitting ? <FiRefreshCw className="animate-spin" /> : <FiPlus />} Create draft
        </button>
        <button type="button" onClick={onClose} className="text-sm font-medium text-slate-500 hover:text-slate-700">
          Cancel
        </button>
      </div>
    </form>
  );
}

const CHANNEL_PILL = 'bg-brand-50 text-brand-600 ring-1 ring-inset ring-brand-100';

function NotificationQueuePanel({ token }) {
  const [jobs, setJobs] = useState([]);
  const [status, setStatus] = useState('loading'); // loading | ready | error
  const [errorMessage, setErrorMessage] = useState('');
  const [busyJobId, setBusyJobId] = useState(null);

  const loadJobs = useCallback(async () => {
    try {
      const data = await getPendingNotificationJobs(token, 25);
      setJobs(data);
      setStatus('ready');
    } catch (err) {
      setErrorMessage(err.message || 'Could not load the notification queue.');
      setStatus('error');
    }
  }, [token]);

  useEffect(() => {
    loadJobs();
    // Poll the outbox-backed queue so newly published/cancelled events show up
    // without the admin needing to refresh the whole dashboard.
    const interval = setInterval(loadJobs, 15000);
    return () => clearInterval(interval);
  }, [loadJobs]);

  const handleComplete = async (job) => {
    setBusyJobId(job.notificationJobId);
    try {
      await completeNotificationJob(token, job.notificationJobId);
      await loadJobs();
    } catch (err) {
      setErrorMessage(err.message || 'Could not mark that job complete.');
    } finally {
      setBusyJobId(null);
    }
  };

  const handleRetry = async (job) => {
    setBusyJobId(job.notificationJobId);
    try {
      await retryNotificationJob(token, job.notificationJobId);
      await loadJobs();
    } catch (err) {
      setErrorMessage(err.message || 'Could not retry that job.');
    } finally {
      setBusyJobId(null);
    }
  };

  return (
    <section className="mb-10 rounded-2xl border border-slate-100 bg-white shadow-card">
      <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
        <div className="flex items-center gap-2">
          <FiBell className="text-brand-500" />
          <h2 className="font-display text-lg font-bold text-ink">Notification queue</h2>
          <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-500">
            {jobs.length} pending
          </span>
        </div>
        <button
          type="button"
          onClick={loadJobs}
          className="flex items-center gap-1.5 rounded-lg border border-slate-200 px-2.5 py-1.5 text-xs font-medium text-slate-500 transition hover:border-brand-400 hover:text-brand-500"
        >
          <FiRefreshCw className={status === 'loading' ? 'animate-spin' : ''} /> Refresh
        </button>
      </div>

      {status === 'error' && (
        <div className="flex items-center gap-2 px-6 py-5 text-sm text-accent-600">
          <FiAlertCircle /> {errorMessage}
        </div>
      )}

      {status === 'ready' && jobs.length === 0 && (
        <div className="flex flex-col items-center gap-2 px-6 py-10 text-center">
          <FiCheckCircle className="text-2xl text-emerald-500" />
          <p className="text-sm font-medium text-slate-500">Queue is empty — every job has been processed.</p>
        </div>
      )}

      {status === 'ready' && jobs.length > 0 && (
        <ul className="divide-y divide-slate-100">
          {jobs.map((job) => (
            <li key={job.notificationJobId} className="flex flex-wrap items-center justify-between gap-3 px-6 py-4">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${CHANNEL_PILL}`}>{job.channel}</span>
                  <p className="truncate text-sm font-semibold text-ink">{job.title}</p>
                </div>
                <p className="mt-1 truncate text-xs text-slate-500">{job.message}</p>
                <p className="mt-1 text-xs text-slate-400">
                  Attempt {job.attempts} · available {new Date(job.availableAt).toLocaleString()}
                </p>
              </div>
              <div className="flex shrink-0 gap-2">
                <ActionButton
                  tone="ghost"
                  icon={<FiRotateCcw />}
                  label="Retry"
                  busy={busyJobId === job.notificationJobId}
                  onClick={() => handleRetry(job)}
                />
                <ActionButton
                  tone="brand"
                  icon={<FiCheck />}
                  label="Complete"
                  busy={busyJobId === job.notificationJobId}
                  onClick={() => handleComplete(job)}
                />
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

const Dashboard = ({ user, token, onLogout }) => {
  const [events, setEvents] = useState([]);
  const [status, setStatus] = useState('loading'); // loading | ready | error
  const [errorMessage, setErrorMessage] = useState('');
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('all');
  const [now, setNow] = useState(new Date());
  const [busyEventId, setBusyEventId] = useState(null);
  const [showNewEventForm, setShowNewEventForm] = useState(false);
  const [toast, setToast] = useState(null); // { tone: 'success' | 'error', text }

  const loadEvents = useCallback(async () => {
    setStatus('loading');
    try {
      const data = await getEvents(token);
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
  }, [token, onLogout]);

  useEffect(() => {
    loadEvents();
  }, [loadEvents]);

  // Keep "Live now" / relative timestamps fresh without re-fetching.
  useEffect(() => {
    const interval = setInterval(() => setNow(new Date()), 60000);
    return () => clearInterval(interval);
  }, []);

  // Auto-dismiss the action toast after a few seconds.
  useEffect(() => {
    if (!toast) return;
    const timeout = setTimeout(() => setToast(null), 4000);
    return () => clearTimeout(timeout);
  }, [toast]);

  const showToast = (tone, text) => setToast({ tone, text });

  const handleCreateEvent = async (payload) => {
    const created = await createEvent(token, payload);
    setEvents((prev) => [created, ...prev]);
    setShowNewEventForm(false);
    showToast('success', `"${created.title}" was created as a draft.`);
  };

  const handlePublish = async (event) => {
    setBusyEventId(event.eventId);
    try {
      const updated = await publishEvent(token, event.eventId);
      setEvents((prev) => prev.map((e) => (e.eventId === updated.eventId ? updated : e)));
      showToast('success', `"${updated.title}" is now published.`);
    } catch (err) {
      showToast('error', err.message || 'Could not publish that event.');
    } finally {
      setBusyEventId(null);
    }
  };

  const handleCancel = async (event) => {
    setBusyEventId(event.eventId);
    try {
      const updated = await cancelEvent(token, event.eventId);
      setEvents((prev) => prev.map((e) => (e.eventId === updated.eventId ? updated : e)));
      showToast('success', `"${updated.title}" was cancelled.`);
    } catch (err) {
      showToast('error', err.message || 'Could not cancel that event.');
    } finally {
      setBusyEventId(null);
    }
  };

  const handleRegister = async (event) => {
    setBusyEventId(event.eventId);
    try {
      await registerForEvent(token, event.eventId);
      showToast('success', `You're registered for "${event.title}".`);
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        showToast('error', "You're already registered for this event.");
      } else {
        showToast('error', err.message || 'Could not register for that event.');
      }
    } finally {
      setBusyEventId(null);
    }
  };

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

  const canCreateEvents = user?.role === 'Teacher' || user?.role === 'Admin';

  return (
    <div className="min-h-screen bg-canvas">
      {toast && (
        <div
          className={`fixed right-6 top-20 z-20 flex items-center gap-2 rounded-xl px-4 py-3 text-sm font-semibold shadow-soft animate-fade-up ${
            toast.tone === 'success' ? 'bg-emerald-50 text-emerald-700 ring-1 ring-inset ring-emerald-200' : 'bg-accent-50 text-accent-600 ring-1 ring-inset ring-accent-100'
          }`}
        >
          {toast.tone === 'success' ? <FiCheckCircle /> : <FiAlertCircle />}
          {toast.text}
        </div>
      )}

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
        <div className="mb-8 flex flex-wrap items-end justify-between gap-4">
          <div>
            <h1 className="font-display text-2xl font-bold text-ink sm:text-3xl">
              Welcome back{user?.displayName ? `, ${user.displayName.split(' ')[0]}` : ''}
            </h1>
            <p className="mt-1 text-slate-500">Here's what's happening across your events.</p>
          </div>
          {canCreateEvents && !showNewEventForm && (
            <button
              type="button"
              onClick={() => setShowNewEventForm(true)}
              className="inline-flex items-center gap-2 rounded-lg bg-brand-500 px-4 py-2 text-sm font-bold text-white shadow-card transition hover:bg-brand-600"
            >
              <FiPlus /> New event
            </button>
          )}
        </div>

        {canCreateEvents && showNewEventForm && (
          <NewEventForm onCreate={handleCreateEvent} onClose={() => setShowNewEventForm(false)} />
        )}

        <div className="mb-8 grid grid-cols-2 gap-4 sm:grid-cols-4">
          <StatCard label="Total events" value={stats.total} accent="text-ink" />
          <StatCard label="Published" value={stats[2]} accent="text-emerald-600" />
          <StatCard label="Draft" value={stats[1]} accent="text-amber-600" />
          <StatCard label="Cancelled" value={stats[3]} accent="text-accent-600" />
        </div>

        {user?.role === 'Admin' && <NotificationQueuePanel token={token} />}

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
              {events.length === 0 ? 'No events yet' : 'No events match your filters'}
            </p>
            <p className="max-w-sm text-sm text-slate-500">
              {events.length === 0
                ? 'Once events are created, they will show up here.'
                : 'Try a different search term or clear the status filter.'}
            </p>
          </div>
        )}

        {status === 'ready' && filteredEvents.length > 0 && (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {filteredEvents.map((event, i) => (
              <EventCard
                key={event.eventId}
                event={event}
                now={now}
                index={i}
                user={user}
                busyEventId={busyEventId}
                onPublish={handlePublish}
                onCancel={handleCancel}
                onRegister={handleRegister}
              />
            ))}
          </div>
        )}
      </main>
    </div>
  );
};

export default Dashboard;
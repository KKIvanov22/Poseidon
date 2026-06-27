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
  FiCheckCircle,
  FiClock,
  FiList,
  FiBell,
} from 'react-icons/fi';
import { 
  getEvents, 
  getMyRegistrations, 
  reserveEventSeat, 
  cancelEventSeat, 
  getEventWaitlist, 
  ApiError 
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

// --- INT-03 & INT-04: Modified EventCard component with multi-role action buttons ---
function EventCard({ event, now, index, userRole, myRegistrations, onRegister, onCancel, onInspectWaitlist, processingId }) {
  const status = STATUS_META[event.eventStatusId] || FALLBACK_STATUS;
  const time = getTimeContext(event.startsAt, event.endsAt, event.eventStatusId, now);
  const isActionable = event.eventStatusId === 2; // Only allow transaction intents on "Published" items

  // Check if current student user has mapped registrations for this event
  const userRegistration = myRegistrations.find(r => r.eventId === event.eventId);

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

      {/* Action Boundary Insertion */}
      <div className="mt-2 border-t border-slate-100 pt-4">
        {userRole === 2 ? (
          /* INT-04: Organizer administrative tools view loop */
          <button
            type="button"
            onClick={() => onInspectWaitlist(event.eventId, event.title)}
            className="flex w-full items-center justify-center gap-2 rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm font-semibold text-slate-700 shadow-sm transition hover:bg-slate-50 hover:text-brand-600 hover:border-brand-300"
          >
            <FiList /> Inspect Waitlist Live Status
          </button>
        ) : (
          /* INT-03: Student user transaction controls flow */
          isActionable ? (
            userRegistration ? (
              <div className="flex items-center justify-between gap-2">
                <span className={`inline-flex items-center gap-1 text-sm font-bold ${userRegistration.statusId === 1 ? 'text-emerald-600' : 'text-amber-500'}`}>
                  {userRegistration.statusId === 1 ? <FiCheckCircle /> : <FiClock />}
                  {userRegistration.statusId === 1 ? 'Confirmed Seat' : 'On Waitlist'}
                </span>
                <button
                  type="button"
                  disabled={processingId === event.eventId}
                  onClick={() => onCancel(event.eventId)}
                  className="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-500 transition hover:bg-accent-50 hover:text-accent-600 hover:border-accent-200 disabled:opacity-50"
                >
                  {processingId === event.eventId ? 'Processing...' : 'Cancel Seat'}
                </button>
              </div>
            ) : (
              <button
                type="button"
                disabled={processingId === event.eventId}
                onClick={() => onRegister(event.eventId)}
                className="flex w-full items-center justify-center gap-2 rounded-lg bg-brand-500 px-4 py-2 text-sm font-bold text-white shadow-card transition hover:bg-brand-600 disabled:opacity-50"
              >
                {processingId === event.eventId ? 'Booking Seat...' : 'Register for Event'}
              </button>
            )
          ) : (
            <button
              type="button"
              disabled
              className="w-full rounded-lg bg-slate-50 border border-slate-100 py-2 text-xs font-medium text-slate-400 cursor-not-allowed"
            >
              Registrations locked
            </button>
          )
        )}
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
  const [myRegistrations, setMyRegistrations] = useState([]);
  const [selectedWaitlist, setSelectedWaitlist] = useState(null);
  const [status, setStatus] = useState('loading'); // loading | ready | error
  const [errorMessage, setErrorMessage] = useState('');
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('all');
  const [now, setNow] = useState(new Date());

  // INT-05: Real-time user alert state configuration variables
  const [systemAlert, setSystemAlert] = useState({ text: '', isError: false });
  const [actionProcessingId, setActionProcessingId] = useState(null);

  // Extract structural properties directly from security matrix
  const userRole = user?.roleId || 1; // 1 = Student, 2 = Organizer

  const triggerSystemAlert = useCallback((text, isError = false) => {
    setSystemAlert({ text, isError });
    setTimeout(() => setSystemAlert({ text: '', isError: false }), 4500);
  }, []);

  // INT-02 & INT-03: Core state fetcher pipeline 
  const loadDashboardMetrics = useCallback(async () => {
    setStatus('loading');
    try {
      // Fetch system events via centralized pipeline
      const data = await getEvents(token);
      setEvents(data);

      // If actor profile resolves to student, catch existing registration structures
      if (userRole === 1) {
        const allocations = await getMyRegistrations(token);
        setMyRegistrations(allocations || []);
      }
      setStatus('ready');
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        onLogout();
        return;
      }
      setErrorMessage(err.message || 'Something went wrong while loading backend pipeline data.');
      setStatus('error');
    }
  }, [token, onLogout, userRole]);

  useEffect(() => {
    loadDashboardMetrics();
  }, [loadDashboardMetrics]);

  // Keep "Live now" / relative timestamps fresh without re-fetching.
  useEffect(() => {
    const interval = setInterval(() => setNow(new Date()), 60000);
    return () => clearInterval(interval);
  }, []);

  // INT-03: Execute seat reservation intent handlers
  const handleEventRegistration = async (eventId) => {
    setActionProcessingId(eventId);
    try {
      const response = await reserveEventSeat(token, eventId);
      
      // INT-05: Catch text properties from response payload
      triggerSystemAlert(response?.message || "Registration transaction resolved successfully!");
      
      // Refresh state indexes to synchronize visual tags
      const updatedAllocations = await getMyRegistrations(token);
      setMyRegistrations(updatedAllocations || []);
      
      // Refresh absolute master totals
      const freshEvents = await getEvents(token);
      setEvents(freshEvents);
    } catch (err) {
      triggerSystemAlert(err.message || "Failed to finalize structural booking allocation parameters.", true);
    } finally {
      setActionProcessingId(null);
    }
  };

  // INT-03: Execute seat cancel intent handlers
  const handleEventCancellation = async (eventId) => {
    setActionProcessingId(eventId);
    try {
      const response = await cancelEventSeat(token, eventId);
      triggerSystemAlert(response?.message || "Your registration status has been cleanly removed.");
      
      const updatedAllocations = await getMyRegistrations(token);
      setMyRegistrations(updatedAllocations || []);

      const freshEvents = await getEvents(token);
      setEvents(freshEvents);
    } catch (err) {
      triggerSystemAlert(err.message || "An exception occurred handling cancellation routine execution.", true);
    } finally {
      setActionProcessingId(null);
    }
  };

  // INT-04: Organizer administrative inspection loop handlers
  const handleInspectWaitlist = async (eventId, title) => {
    try {
      const waitlistData = await getEventWaitlist(token, eventId);
      setSelectedWaitlist({ title, items: waitlistData || [] });
    } catch (err) {
      triggerSystemAlert(err.message || "Could not read queue metrics records.", true);
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
              <p className="text-xs text-slate-400">{userRole === 2 ? 'Organizer Account' : 'Student Account'}</p>
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
        {/* INT-05: Real-time System Toast Alerts Insertion point */}
        {systemAlert.text && (
          <div className={`mb-6 flex items-center gap-3 rounded-xl px-4 py-3 border text-sm font-bold shadow-sm animate-fade-in ${
            systemAlert.isError 
              ? 'border-accent-100 bg-accent-50 text-accent-700' 
              : 'border-emerald-100 bg-emerald-50 text-emerald-700'
          }`}>
            <FiBell className="shrink-0 text-base" />
            <p>System Update: {systemAlert.text}</p>
          </div>
        )}

        <div className="mb-8">
          <h1 className="font-display text-2xl font-bold text-ink sm:text-3xl">
            Welcome back{user?.displayName ? `, ${user.displayName.split(' ')[0]}` : ''}
          </h1>
          <p className="mt-1 text-slate-500">Here's what's happening across your events.</p>
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
              onClick={loadDashboardMetrics}
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
            <p className="font-semibold text-ink">Couldn't load dashboard data</p>
            <p className="max-w-sm text-sm text-slate-500">{errorMessage}</p>
            <button
              type="button"
              onClick={loadDashboardMetrics}
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
                userRole={userRole}
                myRegistrations={myRegistrations}
                onRegister={handleEventRegistration}
                onCancel={handleEventCancellation}
                onInspectWaitlist={handleInspectWaitlist}
                processingId={actionProcessingId}
              />
            ))}
          </div>
        )}

        {/* INT-04: Admin Waitlist Inspector Slide Drawer / Display Block panel */}
        {selectedWaitlist && (
          <div className="mt-10 rounded-2xl border border-slate-200 bg-white p-6 shadow-soft animate-fade-up">
            <div className="flex items-center justify-between border-b border-slate-100 pb-4">
              <div>
                <h3 className="font-display text-lg font-bold text-ink">Waitlist Queue Matrix</h3>
                <p className="text-sm text-slate-500">Reviewing real-time line entries for: <span className="font-semibold text-brand-600">{selectedWaitlist.title}</span></p>
              </div>
              <button
                type="button"
                onClick={() => setSelectedWaitlist(null)}
                className="rounded-lg border border-slate-200 px-3 py-1 text-xs font-semibold text-slate-500 transition hover:bg-slate-50"
              >
                Close Panel
              </button>
            </div>
            
            {selectedWaitlist.items.length === 0 ? (
              <div className="py-8 text-center text-sm text-slate-400 italic">
                There are currently no students in this waitlist queue.
              </div>
            ) : (
              <div className="mt-4 overflow-x-auto">
                <table className="w-full text-left text-sm text-slate-600">
                  <thead className="bg-slate-50 text-xs font-bold uppercase tracking-wider text-slate-500">
                    <tr>
                      <th className="px-4 py-3">Priority No.</th>
                      <th className="px-4 py-3">Student Email Label</th>
                      <th className="px-4 py-3">Queue Entry Time</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {selectedWaitlist.items.map((item, idx) => (
                      <tr key={item.registrationId || idx} className="hover:bg-slate-50/50">
                        <td className="px-4 py-3 font-display font-bold text-brand-600">#{idx + 1}</td>
                        <td className="px-4 py-3 font-medium text-ink">{item.studentEmail || `ID Token: ${item.studentId?.substring(0, 8)}...`}</td>
                        <td className="px-4 py-3 text-slate-400">{item.registeredAt ? new Date(item.registeredAt).toLocaleString() : 'N/A'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}
      </main>
    </div>
  );
};

export default Dashboard;
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  FiAlertCircle,
  FiCheckCircle,
  FiEdit2,
  FiEye,
  FiInbox,
  FiList,
  FiPlus,
  FiRefreshCw,
  FiSearch,
  FiTrash2,
  FiX,
} from 'react-icons/fi';
import {
  cancelEvent,
  createEvent,
  getEventRegistrations,
  getMyEvents,
  publishEvent,
  updateEvent,
} from '../api';
import { useAuth } from '../auth/AuthContext';
import { useApiError } from '../auth/useApiError';
import DashboardLayout from '../components/DashboardLayout';
import CardSkeleton from '../components/events/CardSkeleton';
import EventCard from '../components/events/EventCard';
import StatCard from '../components/events/StatCard';
import { TEACHER_FILTERS } from '../lib/eventUtils';

const INITIAL_CREATE_FORM = {
  title: '',
  description: '',
  startsAt: '',
  endsAt: '',
  capacity: 1,
  locationText: '',
};

function toDateTimeLocal(value) {
  const date = new Date(value);
  const offsetMs = date.getTimezoneOffset() * 60000;
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16);
}

function eventToForm(event) {
  return {
    title: event.title || '',
    description: event.description || '',
    startsAt: toDateTimeLocal(event.startsAt),
    endsAt: toDateTimeLocal(event.endsAt),
    capacity: String(event.capacity || 1),
    locationText: event.locationText || '',
  };
}

function buildEventPayload(form) {
  const title = form.title.trim();
  const capacity = Number(form.capacity);

  if (!title) return { error: 'Event title is required.' };
  if (!form.startsAt || !form.endsAt) return { error: 'Start and end times are required.' };
  if (new Date(form.endsAt) <= new Date(form.startsAt)) {
    return { error: 'The event must end after it starts.' };
  }
  if (!Number.isInteger(capacity) || capacity < 1) {
    return { error: 'Capacity must be at least 1.' };
  }

  return {
    payload: {
      title,
      description: form.description.trim() || null,
      startsAt: new Date(form.startsAt).toISOString(),
      endsAt: new Date(form.endsAt).toISOString(),
      capacity,
      locationText: form.locationText.trim() || null,
    },
  };
}

export default function TeacherDashboard() {
  const { user, token } = useAuth();
  const handleApiError = useApiError();

  const [events, setEvents] = useState([]);
  const [status, setStatus] = useState('loading');
  const [errorMessage, setErrorMessage] = useState('');
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('all');
  const [now, setNow] = useState(new Date());
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [createForm, setCreateForm] = useState(INITIAL_CREATE_FORM);
  const [createStatus, setCreateStatus] = useState('idle');
  const [createError, setCreateError] = useState('');
  const [publishingEventId, setPublishingEventId] = useState(null);
  const [cancellingEventId, setCancellingEventId] = useState(null);
  const [publishError, setPublishError] = useState('');
  const [editingEvent, setEditingEvent] = useState(null);
  const [editForm, setEditForm] = useState(INITIAL_CREATE_FORM);
  const [editStatus, setEditStatus] = useState('idle');
  const [editError, setEditError] = useState('');
  const [previewEvent, setPreviewEvent] = useState(null);
  const [rosterEventId, setRosterEventId] = useState(null);
  const [rosterStatus, setRosterStatus] = useState('idle');
  const [roster, setRoster] = useState(null);
  const [rosterError, setRosterError] = useState('');

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

  const handleCreateChange = (event) => {
    const { name, value } = event.target;
    setCreateForm((prev) => ({
      ...prev,
      [name]: name === 'capacity' ? value.replace(/\D/g, '') : value,
    }));
  };

  const handleEditChange = (event) => {
    const { name, value } = event.target;
    setEditForm((prev) => ({
      ...prev,
      [name]: name === 'capacity' ? value.replace(/\D/g, '') : value,
    }));
  };

  const closeCreateForm = () => {
    setIsCreateOpen(false);
    setCreateError('');
  };

  const handleCreateSubmit = async (event) => {
    event.preventDefault();
    setCreateError('');

    const { payload, error } = buildEventPayload(createForm);
    if (error) {
      setCreateError(error);
      return;
    }

    setCreateStatus('saving');
    try {
      const newEvent = await createEvent(token, payload);
      setEvents((prev) => [newEvent, ...prev]);
      setCreateForm(INITIAL_CREATE_FORM);
      setIsCreateOpen(false);
      setCreateStatus('idle');
    } catch (err) {
      if (handleApiError(err)) {
        setCreateStatus('idle');
        return;
      }
      setCreateError(err.message || 'Something went wrong while creating the event.');
      setCreateStatus('idle');
    }
  };

  const openEditForm = (event) => {
    setEditingEvent(event);
    setEditForm(eventToForm(event));
    setEditError('');
  };

  const closeEditForm = () => {
    setEditingEvent(null);
    setEditError('');
    setEditStatus('idle');
  };

  const handleEditSubmit = async (event) => {
    event.preventDefault();
    if (!editingEvent) return;
    setEditError('');

    const { payload, error } = buildEventPayload(editForm);
    if (error) {
      setEditError(error);
      return;
    }

    setEditStatus('saving');
    try {
      const updated = await updateEvent(token, editingEvent.eventId, payload);
      setEvents((prev) =>
        prev.map((item) => (item.eventId === updated.eventId ? updated : item))
      );
      setPreviewEvent((prev) => (prev?.eventId === updated.eventId ? updated : prev));
      closeEditForm();
    } catch (err) {
      if (handleApiError(err)) {
        setEditStatus('idle');
        return;
      }
      setEditError(err.message || 'Something went wrong while updating the event.');
      setEditStatus('idle');
    }
  };

  const handlePublish = async (eventId) => {
    setPublishingEventId(eventId);
    setPublishError('');

    try {
      const updated = await publishEvent(token, eventId);
      setEvents((prev) =>
        prev.map((event) => (event.eventId === updated.eventId ? updated : event))
      );
    } catch (err) {
      if (handleApiError(err)) return;
      setPublishError(err.message || 'Failed to approve and publish the event.');
    } finally {
      setPublishingEventId(null);
    }
  };

  const handleCancelEvent = async (eventId) => {
    setCancellingEventId(eventId);
    setPublishError('');

    try {
      const updated = await cancelEvent(token, eventId);
      setEvents((prev) =>
        prev.map((event) => (event.eventId === updated.eventId ? updated : event))
      );
      setPreviewEvent((prev) => (prev?.eventId === updated.eventId ? updated : prev));
    } catch (err) {
      if (handleApiError(err)) return;
      setPublishError(err.message || 'Failed to cancel the event.');
    } finally {
      setCancellingEventId(null);
    }
  };

  const handleLoadRoster = async (eventId) => {
    if (rosterEventId === eventId && roster) {
      setRosterEventId(null);
      setRoster(null);
      return;
    }

    setRosterEventId(eventId);
    setRosterStatus('loading');
    setRosterError('');
    try {
      const data = await getEventRegistrations(token, eventId);
      setRoster(data);
      setRosterStatus('ready');
    } catch (err) {
      if (handleApiError(err)) return;
      setRosterError(err.message || 'Failed to load registrations.');
      setRosterStatus('error');
    }
  };

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

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => {
              setIsCreateOpen((value) => !value);
              setCreateError('');
            }}
            className="inline-flex items-center justify-center gap-2 rounded-lg bg-brand-500 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-brand-600"
          >
            {isCreateOpen ? <FiX /> : <FiPlus />}
            {isCreateOpen ? 'Close' : 'Create event'}
          </button>
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

      {isCreateOpen && (
        <form
          onSubmit={handleCreateSubmit}
          className="mb-8 rounded-2xl border border-slate-100 bg-white p-6 shadow-card"
        >
          <div className="mb-5 flex items-start justify-between gap-4">
            <div>
              <h2 className="font-display text-xl font-bold text-ink">Create a draft event</h2>
              <p className="mt-1 text-sm text-slate-500">
                Fill in the details now, then publish the event when it is ready.
              </p>
            </div>
            <button
              type="button"
              onClick={closeCreateForm}
              aria-label="Close create event form"
              className="flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 text-slate-500 transition hover:border-accent-400 hover:text-accent-600"
            >
              <FiX />
            </button>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="md:col-span-2">
              <span className="text-sm font-semibold text-ink">Title</span>
              <input
                name="title"
                value={createForm.title}
                onChange={handleCreateChange}
                required
                className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
                placeholder="Workshop, lecture, meetup..."
              />
            </label>

            <label>
              <span className="text-sm font-semibold text-ink">Starts at</span>
              <input
                type="datetime-local"
                name="startsAt"
                value={createForm.startsAt}
                onChange={handleCreateChange}
                required
                className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
              />
            </label>

            <label>
              <span className="text-sm font-semibold text-ink">Ends at</span>
              <input
                type="datetime-local"
                name="endsAt"
                value={createForm.endsAt}
                onChange={handleCreateChange}
                required
                className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
              />
            </label>

            <label>
              <span className="text-sm font-semibold text-ink">Capacity</span>
              <input
                type="number"
                name="capacity"
                value={createForm.capacity}
                onChange={handleCreateChange}
                required
                min="1"
                className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
              />
            </label>

            <label>
              <span className="text-sm font-semibold text-ink">Location</span>
              <input
                name="locationText"
                value={createForm.locationText}
                onChange={handleCreateChange}
                className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
                placeholder="Room, building, or online link"
              />
            </label>

            <label className="md:col-span-2">
              <span className="text-sm font-semibold text-ink">Description</span>
              <textarea
                name="description"
                value={createForm.description}
                onChange={handleCreateChange}
                rows={3}
                className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
                placeholder="What should students know before attending?"
              />
            </label>
          </div>

          {createError && (
            <div className="mt-4 flex items-center gap-2 rounded-lg border border-accent-100 bg-accent-50 px-3 py-2 text-sm font-medium text-accent-700">
              <FiAlertCircle className="shrink-0" />
              <span>{createError}</span>
            </div>
          )}

          <div className="mt-5 flex flex-wrap items-center justify-end gap-2">
            <button
              type="button"
              onClick={closeCreateForm}
              className="rounded-lg border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-600 transition hover:border-slate-300 hover:bg-slate-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={createStatus === 'saving'}
              className="inline-flex items-center justify-center gap-2 rounded-lg bg-brand-500 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-brand-600 disabled:cursor-not-allowed disabled:opacity-70"
            >
              {createStatus === 'saving' ? (
                <FiRefreshCw className="animate-spin" />
              ) : (
                <FiPlus />
              )}
              {createStatus === 'saving' ? 'Creating...' : 'Create draft'}
            </button>
          </div>
        </form>
      )}

      {editingEvent && (
        <form
          onSubmit={handleEditSubmit}
          className="mb-8 rounded-2xl border border-slate-100 bg-white p-6 shadow-card"
        >
          <div className="mb-5 flex items-start justify-between gap-4">
            <div>
              <h2 className="font-display text-xl font-bold text-ink">Edit draft event</h2>
              <p className="mt-1 text-sm text-slate-500">
                Changes can be saved while the event is still a draft.
              </p>
            </div>
            <button
              type="button"
              onClick={closeEditForm}
              aria-label="Close edit event form"
              className="flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 text-slate-500 transition hover:border-accent-400 hover:text-accent-600"
            >
              <FiX />
            </button>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="md:col-span-2">
              <span className="text-sm font-semibold text-ink">Title</span>
              <input
                name="title"
                value={editForm.title}
                onChange={handleEditChange}
                required
                className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
              />
            </label>

            <label>
              <span className="text-sm font-semibold text-ink">Starts at</span>
              <input
                type="datetime-local"
                name="startsAt"
                value={editForm.startsAt}
                onChange={handleEditChange}
                required
                className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
              />
            </label>

            <label>
              <span className="text-sm font-semibold text-ink">Ends at</span>
              <input
                type="datetime-local"
                name="endsAt"
                value={editForm.endsAt}
                onChange={handleEditChange}
                required
                className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
              />
            </label>

            <label>
              <span className="text-sm font-semibold text-ink">Capacity</span>
              <input
                type="number"
                name="capacity"
                value={editForm.capacity}
                onChange={handleEditChange}
                required
                min="1"
                className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
              />
            </label>

            <label>
              <span className="text-sm font-semibold text-ink">Location</span>
              <input
                name="locationText"
                value={editForm.locationText}
                onChange={handleEditChange}
                className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
              />
            </label>

            <label className="md:col-span-2">
              <span className="text-sm font-semibold text-ink">Description</span>
              <textarea
                name="description"
                value={editForm.description}
                onChange={handleEditChange}
                rows={3}
                className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
              />
            </label>
          </div>

          {editError && (
            <div className="mt-4 flex items-center gap-2 rounded-lg border border-accent-100 bg-accent-50 px-3 py-2 text-sm font-medium text-accent-700">
              <FiAlertCircle className="shrink-0" />
              <span>{editError}</span>
            </div>
          )}

          <div className="mt-5 flex flex-wrap items-center justify-end gap-2">
            <button
              type="button"
              onClick={closeEditForm}
              className="rounded-lg border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-600 transition hover:border-slate-300 hover:bg-slate-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={editStatus === 'saving'}
              className="inline-flex items-center justify-center gap-2 rounded-lg bg-brand-500 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-brand-600 disabled:cursor-not-allowed disabled:opacity-70"
            >
              {editStatus === 'saving' ? <FiRefreshCw className="animate-spin" /> : <FiEdit2 />}
              {editStatus === 'saving' ? 'Saving...' : 'Save changes'}
            </button>
          </div>
        </form>
      )}

      {previewEvent && (
        <div className="mb-8 rounded-2xl border border-slate-100 bg-white p-6 shadow-card">
          <div className="mb-4 flex items-start justify-between gap-4">
            <div>
              <h2 className="font-display text-xl font-bold text-ink">Student preview</h2>
              <p className="mt-1 text-sm text-slate-500">
                This is how the event card appears in the student dashboard.
              </p>
            </div>
            <button
              type="button"
              onClick={() => setPreviewEvent(null)}
              aria-label="Close preview"
              className="flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 text-slate-500 transition hover:border-accent-400 hover:text-accent-600"
            >
              <FiX />
            </button>
          </div>
          <div className="max-w-md">
            <EventCard event={previewEvent} now={now} index={0} />
          </div>
        </div>
      )}

      {rosterEventId && (
        <div className="mb-8 rounded-2xl border border-slate-100 bg-white p-6 shadow-card">
          <div className="mb-4 flex items-start justify-between gap-4">
            <div>
              <h2 className="font-display text-xl font-bold text-ink">Registrations roster</h2>
              <p className="mt-1 text-sm text-slate-500">
                Confirmed students and the ordered waitlist for this event.
              </p>
            </div>
            <button
              type="button"
              onClick={() => {
                setRosterEventId(null);
                setRoster(null);
              }}
              aria-label="Close roster"
              className="flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 text-slate-500 transition hover:border-accent-400 hover:text-accent-600"
            >
              <FiX />
            </button>
          </div>

          {rosterStatus === 'loading' && (
            <p className="text-sm font-medium text-slate-500">Loading roster...</p>
          )}
          {rosterStatus === 'error' && (
            <p className="rounded-lg border border-accent-100 bg-accent-50 px-3 py-2 text-sm text-accent-600">
              {rosterError}
            </p>
          )}
          {rosterStatus === 'ready' && roster && (
            <div className="grid gap-6 lg:grid-cols-2">
              {[
                ['Confirmed', roster.confirmed, `${roster.confirmedCount}/${roster.capacity} seats filled`],
                ['Waitlist', roster.waitlist, `${roster.waitlistCount} waiting`],
              ].map(([label, rows, summary]) => (
                <div key={label} className="overflow-hidden rounded-xl border border-slate-100">
                  <div className="flex items-center justify-between gap-3 bg-slate-50 px-4 py-3">
                    <h3 className="font-semibold text-ink">{label}</h3>
                    <span className="text-xs font-semibold text-slate-500">{summary}</span>
                  </div>
                  {rows.length === 0 ? (
                    <p className="px-4 py-6 text-sm text-slate-500">No students yet.</p>
                  ) : (
                    <div className="divide-y divide-slate-100">
                      {rows.map((row) => (
                        <div key={row.registrationId} className="px-4 py-3">
                          <div className="flex items-center justify-between gap-3">
                            <p className="font-semibold text-ink">{row.studentName || 'Unnamed student'}</p>
                            {row.waitlistPosition && (
                              <span className="rounded-full bg-amber-50 px-2 py-1 text-xs font-semibold text-amber-700">
                                #{row.waitlistPosition}
                              </span>
                            )}
                          </div>
                          <p className="mt-0.5 text-sm text-slate-500">{row.studentEmail}</p>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {publishError && (
        <p className="mb-4 rounded-lg border border-accent-100 bg-accent-50 px-3 py-2 text-sm text-accent-600">
          {publishError}
        </p>
      )}

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
            <EventCard
              key={event.eventId}
              event={event}
              now={now}
              index={i}
              footer={
                <div className="grid grid-cols-2 gap-2">
                  <button
                    type="button"
                    onClick={() => setPreviewEvent(event)}
                    className="inline-flex items-center justify-center gap-2 rounded-lg border border-slate-200 px-3 py-2 text-sm font-semibold text-slate-600 transition hover:border-brand-400 hover:text-brand-600"
                  >
                    <FiEye />
                    Preview
                  </button>

                  <button
                    type="button"
                    onClick={() => handleLoadRoster(event.eventId)}
                    className="inline-flex items-center justify-center gap-2 rounded-lg border border-slate-200 px-3 py-2 text-sm font-semibold text-slate-600 transition hover:border-brand-400 hover:text-brand-600"
                  >
                    <FiList />
                    Roster
                  </button>

                  {event.eventStatusId === 1 && (
                    <>
                      <button
                        type="button"
                        onClick={() => openEditForm(event)}
                        className="inline-flex items-center justify-center gap-2 rounded-lg border border-slate-200 px-3 py-2 text-sm font-semibold text-slate-600 transition hover:border-brand-400 hover:text-brand-600"
                      >
                        <FiEdit2 />
                        Edit
                      </button>
                      <button
                        type="button"
                        disabled={publishingEventId === event.eventId}
                        onClick={() => handlePublish(event.eventId)}
                        className="inline-flex items-center justify-center gap-2 rounded-lg bg-emerald-500 px-3 py-2 text-sm font-semibold text-white transition hover:bg-emerald-600 disabled:cursor-not-allowed disabled:opacity-60"
                      >
                        <FiCheckCircle />
                        {publishingEventId === event.eventId ? 'Publishing...' : 'Publish'}
                      </button>
                    </>
                  )}

                  {event.eventStatusId !== 3 && (
                    <button
                      type="button"
                      disabled={cancellingEventId === event.eventId}
                      onClick={() => handleCancelEvent(event.eventId)}
                      className="col-span-2 inline-flex items-center justify-center gap-2 rounded-lg border border-accent-100 px-3 py-2 text-sm font-semibold text-accent-600 transition hover:border-accent-400 hover:bg-accent-50 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      <FiTrash2 />
                      {cancellingEventId === event.eventId ? 'Cancelling...' : 'Cancel event'}
                    </button>
                  )}
                </div>
              }
            />
          ))}
        </div>
      )}
    </DashboardLayout>
  );
}

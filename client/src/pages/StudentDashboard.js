import { useCallback, useEffect, useMemo, useState } from 'react';
import { FiAlertCircle, FiCheck, FiClock, FiInbox, FiRefreshCw, FiSearch, FiX } from 'react-icons/fi';
import { cancelRegistration, getEvents, getMyRegistrations, registerForEvent } from '../api';
import { useAuth } from '../auth/AuthContext';
import { useApiError } from '../auth/useApiError';
import DashboardLayout from '../components/DashboardLayout';
import CardSkeleton from '../components/events/CardSkeleton';
import EventCard from '../components/events/EventCard';
import { isEventRegisterable } from '../lib/eventUtils';

export default function StudentDashboard() {
  const { user, token } = useAuth();
  const handleApiError = useApiError();

  const [events, setEvents] = useState([]);
  const [status, setStatus] = useState('loading');
  const [errorMessage, setErrorMessage] = useState('');
  const [search, setSearch] = useState('');
  const [now, setNow] = useState(new Date());
  const [registeringId, setRegisteringId] = useState(null);
  const [registrations, setRegistrations] = useState([]);
  const [cancellingId, setCancellingId] = useState(null);
  const [registerErrors, setRegisterErrors] = useState({});

  const loadEvents = useCallback(async () => {
    setStatus('loading');
    try {
      const [data, registrationData] = await Promise.all([
        getEvents(token),
        getMyRegistrations(token),
      ]);
      setEvents(data.filter((e) => e.eventStatusId === 2));
      setRegistrations(registrationData);
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

  const handleRegister = async (eventId) => {
    setRegisteringId(eventId);
    setRegisterErrors((prev) => {
      const next = { ...prev };
      delete next[eventId];
      return next;
    });

    try {
      const registration = await registerForEvent(token, eventId);
      setRegistrations((prev) => [
        {
          registrationId: registration.registrationId,
          eventId: registration.eventId,
          eventTitle: events.find((event) => event.eventId === eventId)?.title || '',
          registrationStatus: registration.registrationStatus,
          waitlistPosition: null,
          registeredAt: registration.registeredAt,
          cancelledAt: null,
        },
        ...prev.filter(
          (item) => !(item.eventId === eventId && item.cancelledAt === null)
        ),
      ]);
      const latestRegistrations = await getMyRegistrations(token);
      setRegistrations(latestRegistrations);
    } catch (err) {
      if (handleApiError(err)) return;
      setRegisterErrors((prev) => ({
        ...prev,
        [eventId]: err.message || 'Registration failed.',
      }));
    } finally {
      setRegisteringId(null);
    }
  };

  const handleCancelRegistration = async (registrationId) => {
    setCancellingId(registrationId);
    setRegisterErrors({});

    try {
      const result = await cancelRegistration(token, registrationId);
      setRegistrations((prev) =>
        prev.map((registration) =>
          registration.registrationId === registrationId
            ? {
                ...registration,
                registrationStatus: 'Cancelled',
                cancelledAt: new Date().toISOString(),
              }
            : registration
        )
      );
      if (result?.promotedRegistrationId) {
        const latestRegistrations = await getMyRegistrations(token);
        setRegistrations(latestRegistrations);
      }
    } catch (err) {
      if (handleApiError(err)) return;
      const eventId = registrations.find((item) => item.registrationId === registrationId)?.eventId;
      if (eventId) {
        setRegisterErrors((prev) => ({
          ...prev,
          [eventId]: err.message || 'Cancellation failed.',
        }));
      }
    } finally {
      setCancellingId(null);
    }
  };

  const filteredEvents = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return events;
    return events.filter(
      (e) =>
        e.title.toLowerCase().includes(q) ||
        (e.locationText || '').toLowerCase().includes(q)
    );
  }, [events, search]);

  const firstName = user?.displayName?.split(' ')[0];
  const activeRegistrationByEventId = useMemo(() => {
    const map = new Map();
    registrations
      .filter((registration) => registration.cancelledAt === null)
      .forEach((registration) => map.set(registration.eventId, registration));
    return map;
  }, [registrations]);

  return (
    <DashboardLayout
      badge="Student dashboard"
      title={`Welcome${firstName ? `, ${firstName}` : ''}`}
      subtitle="Browse published events and register for the ones you want to attend."
    >
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm text-slate-500">
          {events.length} published {events.length === 1 ? 'event' : 'events'} available
        </p>
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
            {events.length === 0 ? 'No published events yet' : 'No events match your search'}
          </p>
          <p className="max-w-sm text-sm text-slate-500">
            Check back later — teachers publish events here when they are ready.
          </p>
        </div>
      )}

      {status === 'ready' && filteredEvents.length > 0 && (
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {filteredEvents.map((event, i) => {
            const activeRegistration = activeRegistrationByEventId.get(event.eventId);
            const canRegister = isEventRegisterable(event, now);
            const isRegistering = registeringId === event.eventId;
            const isCancelling = activeRegistration && cancellingId === activeRegistration.registrationId;
            const registerError = registerErrors[event.eventId];

            return (
              <EventCard
                key={event.eventId}
                event={event}
                now={now}
                index={i}
                footer={
                  <div className="space-y-2">
                    {registerError && (
                      <p className="text-xs font-medium text-accent-600">{registerError}</p>
                    )}
                    {activeRegistration ? (
                      <div className="space-y-2">
                        <div
                          className={`flex items-center justify-center gap-2 rounded-lg px-4 py-2.5 text-sm font-semibold ${
                            activeRegistration.registrationStatus === 'Waitlisted'
                              ? 'bg-amber-50 text-amber-700'
                              : 'bg-emerald-50 text-emerald-700'
                          }`}
                        >
                          {activeRegistration.registrationStatus === 'Waitlisted' ? <FiClock /> : <FiCheck />}
                          {activeRegistration.registrationStatus === 'Waitlisted'
                            ? `Waitlisted${activeRegistration.waitlistPosition ? ` #${activeRegistration.waitlistPosition}` : ''}`
                            : 'Registered'}
                        </div>
                        <button
                          type="button"
                          disabled={isCancelling}
                          onClick={() => handleCancelRegistration(activeRegistration.registrationId)}
                          className="inline-flex w-full items-center justify-center gap-2 rounded-lg border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-600 transition hover:border-accent-400 hover:text-accent-600 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                          <FiX />
                          {isCancelling ? 'Cancelling...' : 'Cancel registration'}
                        </button>
                      </div>
                    ) : (
                      <button
                        type="button"
                        disabled={!canRegister || isRegistering}
                        onClick={() => handleRegister(event.eventId)}
                        className="w-full rounded-lg bg-brand-500 px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-brand-600 disabled:cursor-not-allowed disabled:opacity-50"
                      >
                        {isRegistering
                          ? 'Registering...'
                          : canRegister
                            ? 'Register'
                            : 'Registration closed'}
                      </button>
                    )}
                  </div>
                }
              />
            );
          })}
        </div>
      )}
    </DashboardLayout>
  );
}

import { FiCalendar, FiMapPin, FiUsers } from 'react-icons/fi';
import { useTheme } from '../../theme/ThemeContext';
import {
  STATUS_META,
  FALLBACK_STATUS,
  getTimeContext,
  formatDateRange,
} from '../../lib/eventUtils';

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

export default function EventCard({ event, now, index, footer }) {
  const status = STATUS_META[event.eventStatusId] || FALLBACK_STATUS;
  const time = getTimeContext(event.startsAt, event.endsAt, event.eventStatusId, now);
  const { isDark } = useTheme();

  return (
    <div
      className={`group flex flex-col gap-4 rounded-2xl border p-6 shadow-card transition hover:-translate-y-0.5 hover:shadow-card-hover animate-fade-up ${isDark ? 'border-slate-800 bg-slate-900' : 'border-slate-100 bg-white'}`}
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
        <h3 className={`font-display text-lg font-bold leading-snug line-clamp-2 ${isDark ? 'text-slate-100' : 'text-ink'}`}>{event.title}</h3>
        <p className={`mt-1 line-clamp-2 text-sm ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
          {event.description || <span className={`italic ${isDark ? 'text-slate-500' : 'text-slate-400'}`}>No description provided.</span>}
        </p>
      </div>

      <div className={`space-y-2 border-t pt-4 text-sm ${isDark ? 'border-slate-800 text-slate-300' : 'border-slate-100 text-slate-600'}`}>
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

      {footer && <div className="mt-auto pt-2">{footer}</div>}
    </div>
  );
}

export const STATUS_META = {
  1: { label: 'Draft', dot: 'bg-amber-400', pill: 'bg-amber-50 text-amber-700 ring-amber-200' },
  2: { label: 'Published', dot: 'bg-emerald-500', pill: 'bg-emerald-50 text-emerald-700 ring-emerald-200' },
  3: { label: 'Cancelled', dot: 'bg-accent', pill: 'bg-accent-50 text-accent-600 ring-accent-100' },
  4: { label: 'Completed', dot: 'bg-slate-400', pill: 'bg-slate-100 text-slate-600 ring-slate-200' },
};

export const FALLBACK_STATUS = {
  label: 'Unknown',
  dot: 'bg-slate-400',
  pill: 'bg-slate-100 text-slate-600 ring-slate-200',
};

export const TEACHER_FILTERS = [
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

export function getTimeContext(startsAt, endsAt, statusId, now) {
  if (statusId === 3) return { label: 'Cancelled', tone: 'muted' };
  const start = new Date(startsAt);
  const end = new Date(endsAt);
  if (now < start) return { label: `Starts ${relativeLabel(start, now)}`, tone: 'upcoming' };
  if (now <= end) return { label: 'Live now', tone: 'live' };
  return { label: `Ended ${relativeLabel(end, now)}`, tone: 'ended' };
}

export function formatDateRange(startsAt, endsAt) {
  const start = new Date(startsAt);
  const end = new Date(endsAt);
  const dateFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' });
  const timeFmt = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' });
  const sameDay = start.toDateString() === end.toDateString();
  return sameDay
    ? `${dateFmt.format(start)} · ${timeFmt.format(start)} – ${timeFmt.format(end)}`
    : `${dateFmt.format(start)} ${timeFmt.format(start)} – ${dateFmt.format(end)} ${timeFmt.format(end)}`;
}

export function isEventRegisterable(event, now = new Date()) {
  return event.eventStatusId === 2 && new Date(event.endsAt) > now;
}

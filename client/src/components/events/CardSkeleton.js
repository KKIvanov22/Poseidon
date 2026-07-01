export default function CardSkeleton() {
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

export default function StatCard({ label, value, accent }) {
  return (
    <div className="rounded-2xl border border-slate-100 bg-white p-5 shadow-card">
      <p className="text-sm font-medium text-slate-500">{label}</p>
      <p className={`mt-1 font-display text-3xl font-bold tabular-nums ${accent}`}>{value}</p>
    </div>
  );
}

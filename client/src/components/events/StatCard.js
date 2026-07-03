import { useTheme } from '../../theme/ThemeContext';

export default function StatCard({ label, value, accent }) {
  const { isDark } = useTheme();

  return (
    <div className={`rounded-2xl border p-5 shadow-card ${isDark ? 'border-slate-800 bg-slate-900' : 'border-slate-100 bg-white'}`}>
      <p className={`text-sm font-medium ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>{label}</p>
      <p className={`mt-1 font-display text-3xl font-bold tabular-nums ${accent}`}>{value}</p>
    </div>
  );
}

import { useTheme } from '../../theme/ThemeContext';

export default function CardSkeleton() {
  const { isDark } = useTheme();

  return (
    <div className={`animate-pulse rounded-2xl border p-6 shadow-card ${isDark ? 'border-slate-800 bg-slate-900' : 'border-slate-100 bg-white'}`}>
      <div className="flex justify-between">
        <div className={`h-5 w-20 rounded-full ${isDark ? 'bg-slate-800' : 'bg-slate-100'}`} />
        <div className={`h-5 w-24 rounded-full ${isDark ? 'bg-slate-800' : 'bg-slate-100'}`} />
      </div>
      <div className={`mt-4 h-5 w-3/4 rounded ${isDark ? 'bg-slate-800' : 'bg-slate-100'}`} />
      <div className={`mt-2 h-4 w-full rounded ${isDark ? 'bg-slate-800' : 'bg-slate-100'}`} />
      <div className={`mt-1 h-4 w-2/3 rounded ${isDark ? 'bg-slate-800' : 'bg-slate-100'}`} />
      <div className={`mt-6 space-y-2 border-t pt-4 ${isDark ? 'border-slate-800' : 'border-slate-100'}`}>
        <div className={`h-3.5 w-1/2 rounded ${isDark ? 'bg-slate-800' : 'bg-slate-100'}`} />
        <div className={`h-3.5 w-1/3 rounded ${isDark ? 'bg-slate-800' : 'bg-slate-100'}`} />
        <div className={`h-3.5 w-2/5 rounded ${isDark ? 'bg-slate-800' : 'bg-slate-100'}`} />
      </div>
    </div>
  );
}

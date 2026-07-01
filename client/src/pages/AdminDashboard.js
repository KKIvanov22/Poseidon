import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  FiAlertCircle,
  FiRefreshCw,
  FiSearch,
  FiShield,
  FiUsers,
  FiBell,
} from 'react-icons/fi';
import {
  listUsers,
  updateUserRole,
  getPendingNotificationJobs,
  completeNotificationJob,
  retryNotificationJob,
} from '../api';
import { useAuth } from '../auth/AuthContext';
import { useApiError } from '../auth/useApiError';
import DashboardLayout from '../components/DashboardLayout';
import { normalizeRole, ROLES } from '../lib/roles';

const TABS = [
  { key: 'users', label: 'User management', icon: FiUsers },
  { key: 'jobs', label: 'Notification jobs', icon: FiBell },
];

function RoleBadge({ role }) {
  const normalized = normalizeRole(role);
  const styles = {
    [ROLES.ADMIN]: 'bg-purple-50 text-purple-700 ring-purple-200',
    [ROLES.TEACHER]: 'bg-brand-50 text-brand-700 ring-brand-200',
    [ROLES.STUDENT]: 'bg-slate-100 text-slate-600 ring-slate-200',
  };
  return (
    <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ring-1 ring-inset ${styles[normalized] || styles[ROLES.STUDENT]}`}>
      {role}
    </span>
  );
}

export default function AdminDashboard() {
  const { user, token } = useAuth();
  const handleApiError = useApiError();

  const [activeTab, setActiveTab] = useState('users');
  const [users, setUsers] = useState([]);
  const [jobs, setJobs] = useState([]);
  const [usersStatus, setUsersStatus] = useState('loading');
  const [jobsStatus, setJobsStatus] = useState('idle');
  const [usersError, setUsersError] = useState('');
  const [jobsError, setJobsError] = useState('');
  const [search, setSearch] = useState('');
  const [updatingUserId, setUpdatingUserId] = useState(null);
  const [actionJobId, setActionJobId] = useState(null);

  const loadUsers = useCallback(async () => {
    setUsersStatus('loading');
    try {
      const data = await listUsers(token);
      setUsers(data);
      setUsersStatus('ready');
    } catch (err) {
      if (handleApiError(err)) return;
      setUsersError(err.message || 'Failed to load users.');
      setUsersStatus('error');
    }
  }, [token, handleApiError]);

  const loadJobs = useCallback(async () => {
    setJobsStatus('loading');
    try {
      const data = await getPendingNotificationJobs(token);
      setJobs(data);
      setJobsStatus('ready');
    } catch (err) {
      if (handleApiError(err)) return;
      setJobsError(err.message || 'Failed to load notification jobs.');
      setJobsStatus('error');
    }
  }, [token, handleApiError]);

  useEffect(() => {
    loadUsers();
  }, [loadUsers]);

  useEffect(() => {
    if (activeTab === 'jobs' && jobsStatus === 'idle') {
      loadJobs();
    }
  }, [activeTab, jobsStatus, loadJobs]);

  const handleRoleChange = async (targetUser) => {
    const currentRole = normalizeRole(targetUser.role);
    if (currentRole === ROLES.ADMIN) return;

    const newRole = currentRole === ROLES.STUDENT ? 'Teacher' : 'Student';
    setUpdatingUserId(targetUser.userId);

    try {
      const updated = await updateUserRole(token, targetUser.userId, newRole);
      setUsers((prev) =>
        prev.map((u) => (u.userId === updated.userId ? updated : u))
      );
    } catch (err) {
      if (handleApiError(err)) return;
      setUsersError(err.message || 'Failed to update role.');
    } finally {
      setUpdatingUserId(null);
    }
  };

  const handleCompleteJob = async (jobId) => {
    setActionJobId(jobId);
    try {
      await completeNotificationJob(token, jobId);
      setJobs((prev) => prev.filter((j) => j.notificationJobId !== jobId));
    } catch (err) {
      if (handleApiError(err)) return;
      setJobsError(err.message || 'Failed to complete job.');
    } finally {
      setActionJobId(null);
    }
  };

  const handleRetryJob = async (jobId) => {
    setActionJobId(jobId);
    try {
      await retryNotificationJob(token, jobId);
      await loadJobs();
    } catch (err) {
      if (handleApiError(err)) return;
      setJobsError(err.message || 'Failed to retry job.');
    } finally {
      setActionJobId(null);
    }
  };

  const filteredUsers = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return users;
    return users.filter(
      (u) =>
        u.displayName.toLowerCase().includes(q) ||
        u.email.toLowerCase().includes(q) ||
        u.role.toLowerCase().includes(q)
    );
  }, [users, search]);

  const manageableUsers = filteredUsers.filter(
    (u) => normalizeRole(u.role) !== ROLES.ADMIN
  );

  const firstName = user?.displayName?.split(' ')[0];

  return (
    <DashboardLayout
      badge="Admin dashboard"
      title={`Admin panel${firstName ? ` — ${firstName}` : ''}`}
      subtitle="Manage user roles and monitor system notification jobs."
    >
      <div className="mb-6 flex flex-wrap gap-2">
        {TABS.map(({ key, label, icon: Icon }) => (
          <button
            key={key}
            type="button"
            onClick={() => setActiveTab(key)}
            className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${
              activeTab === key
                ? 'bg-brand-500 text-white shadow-sm'
                : 'bg-white text-slate-600 ring-1 ring-inset ring-slate-200 hover:bg-slate-50'
            }`}
          >
            <Icon /> {label}
          </button>
        ))}
      </div>

      {activeTab === 'users' && (
        <section aria-labelledby="users-heading">
          <div className="mb-4 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-2 text-sm text-slate-500">
              <FiShield className="text-brand-500" />
              <span>Promote students to teachers or demote teachers to students.</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="relative">
                <FiSearch className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
                <input
                  type="search"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Search users"
                  aria-label="Search users"
                  className="w-full rounded-lg border border-slate-200 bg-white py-2 pl-9 pr-3 text-sm text-ink outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100 sm:w-64"
                />
              </div>
              <button
                type="button"
                onClick={loadUsers}
                aria-label="Refresh users"
                className="flex items-center justify-center rounded-lg border border-slate-200 p-2 text-slate-500 transition hover:border-brand-400 hover:text-brand-500"
              >
                <FiRefreshCw className={usersStatus === 'loading' ? 'animate-spin' : ''} />
              </button>
            </div>
          </div>

          {usersError && usersStatus !== 'loading' && (
            <p className="mb-4 rounded-lg border border-accent-100 bg-accent-50 px-3 py-2 text-sm text-accent-600">
              {usersError}
            </p>
          )}

          {usersStatus === 'error' && (
            <div className="flex flex-col items-center gap-3 rounded-2xl border border-accent-100 bg-accent-50 px-6 py-14 text-center">
              <FiAlertCircle className="text-3xl text-accent-600" />
              <p className="font-semibold text-ink">Couldn&apos;t load users</p>
              <p className="max-w-sm text-sm text-slate-500">{usersError}</p>
              <button
                type="button"
                onClick={loadUsers}
                className="mt-2 rounded-lg bg-brand-500 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-600"
              >
                Try again
              </button>
            </div>
          )}

          {usersStatus === 'loading' && (
            <div className="animate-pulse space-y-3">
              {Array.from({ length: 5 }).map((_, i) => (
                <div key={i} className="h-16 rounded-xl bg-white shadow-card" />
              ))}
            </div>
          )}

          {usersStatus === 'ready' && (
            <div className="overflow-hidden rounded-2xl border border-slate-100 bg-white shadow-card">
              <table className="w-full text-left text-sm">
                <thead className="border-b border-slate-100 bg-slate-50/80">
                  <tr>
                    <th className="px-5 py-3 font-semibold text-slate-600">Name</th>
                    <th className="hidden px-5 py-3 font-semibold text-slate-600 sm:table-cell">Email</th>
                    <th className="px-5 py-3 font-semibold text-slate-600">Role</th>
                    <th className="px-5 py-3 font-semibold text-slate-600">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {manageableUsers.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="px-5 py-10 text-center text-slate-500">
                        No manageable users found.
                      </td>
                    </tr>
                  ) : (
                    manageableUsers.map((u) => {
                      const role = normalizeRole(u.role);
                      const isSelf = u.userId === user?.userId;
                      const isUpdating = updatingUserId === u.userId;
                      const toggleLabel =
                        role === ROLES.STUDENT ? 'Make Teacher' : 'Make Student';

                      return (
                        <tr key={u.userId} className="hover:bg-slate-50/50">
                          <td className="px-5 py-4">
                            <p className="font-semibold text-ink">{u.displayName}</p>
                            <p className="mt-0.5 text-xs text-slate-400 sm:hidden">{u.email}</p>
                          </td>
                          <td className="hidden px-5 py-4 text-slate-600 sm:table-cell">{u.email}</td>
                          <td className="px-5 py-4">
                            <RoleBadge role={u.role} />
                          </td>
                          <td className="px-5 py-4">
                            {isSelf ? (
                              <span className="text-xs text-slate-400">You</span>
                            ) : (
                              <button
                                type="button"
                                disabled={isUpdating}
                                onClick={() => handleRoleChange(u)}
                                className="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-700 transition hover:border-brand-400 hover:text-brand-600 disabled:opacity-50"
                              >
                                {isUpdating ? 'Updating...' : toggleLabel}
                              </button>
                            )}
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {activeTab === 'jobs' && (
        <section aria-labelledby="jobs-heading">
          <div className="mb-4 flex items-center justify-between">
            <p className="text-sm text-slate-500">
              Pending notification jobs awaiting processing.
            </p>
            <button
              type="button"
              onClick={loadJobs}
              aria-label="Refresh jobs"
              className="flex items-center gap-2 rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-500 transition hover:border-brand-400 hover:text-brand-500"
            >
              <FiRefreshCw className={jobsStatus === 'loading' ? 'animate-spin' : ''} />
              Refresh
            </button>
          </div>

          {jobsError && jobsStatus !== 'loading' && (
            <p className="mb-4 rounded-lg border border-accent-100 bg-accent-50 px-3 py-2 text-sm text-accent-600">
              {jobsError}
            </p>
          )}

          {jobsStatus === 'error' && (
            <div className="flex flex-col items-center gap-3 rounded-2xl border border-accent-100 bg-accent-50 px-6 py-14 text-center">
              <FiAlertCircle className="text-3xl text-accent-600" />
              <p className="font-semibold text-ink">Couldn&apos;t load jobs</p>
              <button
                type="button"
                onClick={loadJobs}
                className="mt-2 rounded-lg bg-brand-500 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-600"
              >
                Try again
              </button>
            </div>
          )}

          {jobsStatus === 'loading' && (
            <div className="animate-pulse space-y-3">
              {Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="h-24 rounded-xl bg-white shadow-card" />
              ))}
            </div>
          )}

          {jobsStatus === 'ready' && jobs.length === 0 && (
            <div className="rounded-2xl border border-slate-100 bg-white px-6 py-14 text-center text-slate-500">
              No pending notification jobs.
            </div>
          )}

          {jobsStatus === 'ready' && jobs.length > 0 && (
            <div className="space-y-4">
              {jobs.map((job) => {
                const isActing = actionJobId === job.notificationJobId;
                return (
                  <div
                    key={job.notificationJobId}
                    className="rounded-2xl border border-slate-100 bg-white p-5 shadow-card"
                  >
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div>
                        <p className="font-semibold text-ink">{job.title}</p>
                        <p className="mt-1 text-sm text-slate-500">{job.message}</p>
                        <p className="mt-2 text-xs text-slate-400">
                          Channel: {job.channel} · Attempts: {job.attempts}
                        </p>
                      </div>
                      <div className="flex gap-2">
                        <button
                          type="button"
                          disabled={isActing}
                          onClick={() => handleCompleteJob(job.notificationJobId)}
                          className="rounded-lg bg-emerald-500 px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-emerald-600 disabled:opacity-50"
                        >
                          Complete
                        </button>
                        <button
                          type="button"
                          disabled={isActing}
                          onClick={() => handleRetryJob(job.notificationJobId)}
                          className="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-700 transition hover:border-brand-400 disabled:opacity-50"
                        >
                          Retry
                        </button>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </section>
      )}
    </DashboardLayout>
  );
}

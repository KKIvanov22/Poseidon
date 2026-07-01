export const ROLES = {
  STUDENT: 'student',
  TEACHER: 'teacher',
  ADMIN: 'admin',
};

export function normalizeRole(role) {
  return (role || '').trim().toLowerCase();
}

export function getDashboardPath(role) {
  switch (normalizeRole(role)) {
    case ROLES.ADMIN:
      return '/dashboard/admin';
    case ROLES.TEACHER:
      return '/dashboard/teacher';
    case ROLES.STUDENT:
    default:
      return '/dashboard/student';
  }
}

export function isRoleAllowed(userRole, allowedRoles) {
  if (!allowedRoles?.length) return true;
  return allowedRoles.includes(normalizeRole(userRole));
}

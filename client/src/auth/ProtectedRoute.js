import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from './AuthContext';
import { getDashboardPath, isRoleAllowed } from '../lib/roles';

export default function ProtectedRoute({ children, allowedRoles }) {
  const { isAuthenticated, user } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  if (!isRoleAllowed(user?.role, allowedRoles)) {
    return <Navigate to={getDashboardPath(user?.role)} replace />;
  }

  return children;
}

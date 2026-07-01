import { useCallback } from 'react';
import { ApiError } from '../api';
import { useAuth } from './AuthContext';

export function useApiError() {
  const { logout } = useAuth();

  return useCallback(
    (error) => {
      if (error instanceof ApiError && error.status === 401) {
        logout();
        return true;
      }
      return false;
    },
    [logout]
  );
}

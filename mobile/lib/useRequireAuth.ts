import { useEffect } from 'react';
import { useRouter, useRootNavigationState } from 'expo-router';
import { getAuthSession } from './auth';

export function useRequireAuth() {
  const router = useRouter();
  const navigationState = useRootNavigationState();
  const session = getAuthSession();
  const ready = Boolean(navigationState?.key);
  const authenticated = Boolean(session?.loggedIn);

  useEffect(() => {
    if (!ready || authenticated) return;
    router.replace('/login');
  }, [ready, authenticated, router]);

  return { session: authenticated ? session : null, ready, authenticated };
}

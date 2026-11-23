"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useSession } from "./session";

/** Sends logged-out visitors to /login. Returns the session (undefined while loading). */
export function useRequireAuth() {
  const session = useSession();
  const router = useRouter();

  useEffect(() => {
    if (session === null) router.replace("/login");
  }, [session, router]);

  return session;
}

/** Sends already logged-in visitors away from /login and /register. */
export function useRedirectIfAuthenticated(to = "/products") {
  const session = useSession();
  const router = useRouter();

  useEffect(() => {
    if (session) router.replace(to);
  }, [session, router, to]);

  return session;
}

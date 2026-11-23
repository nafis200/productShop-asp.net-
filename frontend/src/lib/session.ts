"use client";

import { useSyncExternalStore } from "react";
import type { AuthResponse } from "./types";

// The logged-in session lives in localStorage so it survives page reloads.
// useSyncExternalStore keeps every component in sync with it (and with other tabs).

export type Session = Pick<AuthResponse, "accessToken" | "expiresAt" | "user">;

const STORAGE_KEY = "productshop.session";
const listeners = new Set<() => void>();

let cachedRaw: string | null | undefined;
let cachedSession: Session | null = null;

function readRaw(): string | null {
  try {
    return window.localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function parse(raw: string | null): Session | null {
  if (!raw) return null;
  try {
    const session = JSON.parse(raw) as Session;
    if (!session.accessToken || new Date(session.expiresAt).getTime() <= Date.now()) return null;
    return session;
  } catch {
    return null;
  }
}

function getSnapshot(): Session | null {
  const raw = readRaw();
  if (raw !== cachedRaw) {
    cachedRaw = raw;
    cachedSession = parse(raw);
  }
  return cachedSession;
}

// On the server (and during hydration) we don't know yet: undefined = "still loading".
function getServerSnapshot(): undefined {
  return undefined;
}

function subscribe(onChange: () => void) {
  listeners.add(onChange);
  window.addEventListener("storage", onChange);
  return () => {
    listeners.delete(onChange);
    window.removeEventListener("storage", onChange);
  };
}

function emit() {
  listeners.forEach((listener) => listener());
}

export function saveSession(auth: AuthResponse) {
  const session: Session = { accessToken: auth.accessToken, expiresAt: auth.expiresAt, user: auth.user };
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  } catch {
    // storage blocked: the session will only last until reload
    cachedRaw = JSON.stringify(session);
    cachedSession = session;
  }
  emit();
}

export function clearSession() {
  try {
    window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    cachedRaw = null;
    cachedSession = null;
  }
  emit();
}

/** undefined = loading, null = logged out, Session = logged in */
export function useSession(): Session | null | undefined {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}

export function isAdmin(session: Session | null | undefined): boolean {
  return !!session?.user.roles.includes("Admin");
}

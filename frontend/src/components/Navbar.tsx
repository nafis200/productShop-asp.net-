"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { clearSession, isAdmin, useSession } from "@/lib/session";
import { Button } from "./ui";

export function Navbar() {
  const session = useSession();
  const router = useRouter();

  function logout() {
    clearSession();
    router.replace("/login");
  }

  return (
    <header className="border-b border-zinc-200 bg-white/80 backdrop-blur dark:border-zinc-800 dark:bg-zinc-950/80">
      <nav className="mx-auto flex h-16 max-w-5xl items-center justify-between px-4">
        <Link href="/products" className="flex items-center gap-2 font-bold tracking-tight">
          <span className="grid size-8 place-items-center rounded-lg bg-indigo-600 text-sm text-white">PS</span>
          <span>ProductShop</span>
        </Link>

        {session === undefined ? null : session ? (
          <div className="flex items-center gap-3">
            <div className="hidden text-right sm:block">
              <p className="text-sm font-medium leading-tight">
                {session.user.firstName} {session.user.lastName}
              </p>
              <p className="text-xs text-zinc-500">{session.user.email}</p>
            </div>
            <span
              className={
                isAdmin(session)
                  ? "rounded-full bg-indigo-100 px-2.5 py-0.5 text-xs font-semibold text-indigo-700 dark:bg-indigo-950 dark:text-indigo-300"
                  : "rounded-full bg-zinc-100 px-2.5 py-0.5 text-xs font-semibold text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300"
              }
            >
              {isAdmin(session) ? "Admin" : "User"}
            </span>
            <Button variant="secondary" onClick={logout}>
              Log out
            </Button>
          </div>
        ) : (
          <div className="flex items-center gap-2">
            <Link href="/login" className="rounded-lg px-3 py-2 text-sm font-semibold hover:bg-zinc-100 dark:hover:bg-zinc-800">
              Log in
            </Link>
            <Link
              href="/register"
              className="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
            >
              Register
            </Link>
          </div>
        )}
      </nav>
    </header>
  );
}

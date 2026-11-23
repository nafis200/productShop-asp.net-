import Link from "next/link";
import type { ReactNode } from "react";
import { Card } from "./ui";

export function AuthCard({
  title,
  subtitle,
  footer,
  children,
}: {
  title: string;
  subtitle: string;
  footer: { text: string; linkText: string; href: string };
  children: ReactNode;
}) {
  return (
    <div className="flex flex-1 items-start justify-center px-4 py-12 sm:py-20">
      <div className="w-full max-w-md">
        <div className="mb-6 text-center">
          <h1 className="text-2xl font-bold tracking-tight">{title}</h1>
          <p className="mt-1 text-sm text-zinc-500">{subtitle}</p>
        </div>
        <Card className="p-6 sm:p-8">{children}</Card>
        <p className="mt-6 text-center text-sm text-zinc-500">
          {footer.text}{" "}
          <Link href={footer.href} className="font-semibold text-indigo-600 hover:text-indigo-500">
            {footer.linkText}
          </Link>
        </p>
      </div>
    </div>
  );
}

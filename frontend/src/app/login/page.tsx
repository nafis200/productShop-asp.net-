"use client";

import { useState, type FormEvent } from "react";
import { AuthCard } from "@/components/AuthCard";
import { Alert, Button, Field } from "@/components/ui";
import { api, ApiError } from "@/lib/api";
import { saveSession } from "@/lib/session";
import { useRedirectIfAuthenticated } from "@/lib/use-redirect";

export default function LoginPage() {
  useRedirectIfAuthenticated();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const auth = await api.login({ email: email.trim(), password });
      setPassword("");
      saveSession(auth); // useRedirectIfAuthenticated then moves us to /products
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <AuthCard
      title="Welcome back"
      subtitle="Log in to see the product list."
      footer={{ text: "No account yet?", linkText: "Create one", href: "/register" }}
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        {error && <Alert>{error}</Alert>}
        <Field
          id="email"
          label="Email"
          type="email"
          autoComplete="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />
        <Field
          id="password"
          label="Password"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        <Button type="submit" loading={submitting} className="w-full">
          Log in
        </Button>
      </form>
    </AuthCard>
  );
}

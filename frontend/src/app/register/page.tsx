"use client";

import { useState, type FormEvent } from "react";
import { AuthCard } from "@/components/AuthCard";
import { Alert, Button, Field } from "@/components/ui";
import { api, ApiError } from "@/lib/api";
import { saveSession } from "@/lib/session";
import { useRedirectIfAuthenticated } from "@/lib/use-redirect";

const emptyForm = { firstName: "", lastName: "", email: "", password: "", confirmPassword: "" };

export default function RegisterPage() {
  useRedirectIfAuthenticated();

  const [form, setForm] = useState(emptyForm);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  function update(field: keyof typeof emptyForm) {
    return (e: React.ChangeEvent<HTMLInputElement>) => setForm((f) => ({ ...f, [field]: e.target.value }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    if (form.password !== form.confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    setSubmitting(true);
    try {
      const auth = await api.register({
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        password: form.password,
      });
      setForm(emptyForm);
      saveSession(auth);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <AuthCard
      title="Create your account"
      subtitle="New accounts can view products. Admins manage them."
      footer={{ text: "Already registered?", linkText: "Log in", href: "/login" }}
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        {error && <Alert>{error}</Alert>}
        <div className="grid gap-4 sm:grid-cols-2">
          <Field id="firstName" label="First name" autoComplete="given-name" required maxLength={50}
            value={form.firstName} onChange={update("firstName")} />
          <Field id="lastName" label="Last name" autoComplete="family-name" required maxLength={50}
            value={form.lastName} onChange={update("lastName")} />
        </div>
        <Field id="email" label="Email" type="email" autoComplete="email" required
          value={form.email} onChange={update("email")} />
        <Field id="password" label="Password" type="password" autoComplete="new-password" required
          minLength={8} maxLength={100} placeholder="At least 8 characters"
          value={form.password} onChange={update("password")} />
        <Field id="confirmPassword" label="Confirm password" type="password" autoComplete="new-password" required
          value={form.confirmPassword} onChange={update("confirmPassword")} />
        <Button type="submit" loading={submitting} className="w-full">
          Create account
        </Button>
      </form>
    </AuthCard>
  );
}

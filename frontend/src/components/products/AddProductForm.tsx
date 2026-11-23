"use client";

import { useState, type FormEvent } from "react";
import { Alert, Button, Field } from "@/components/ui";
import { parsePrice } from "@/lib/format";
import type { ProductInput } from "@/lib/types";

export function AddProductForm({ onAdd }: { onAdd: (input: ProductInput) => Promise<void> }) {
  const [name, setName] = useState("");
  const [price, setPrice] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    const parsedPrice = parsePrice(price);
    if (!name.trim()) return setError("Product name is required.");
    if (parsedPrice === null) return setError("Price must be between 0.01 and 1,000,000.");

    setSubmitting(true);
    try {
      await onAdd({ name: name.trim(), price: parsedPrice });
      setName("");
      setPrice("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not add the product.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <Field
          id="product-name"
          label="Product name"
          className="flex-1"
          placeholder="e.g. Mechanical keyboard"
          maxLength={100}
          required
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
        <Field
          id="product-price"
          label="Price"
          className="sm:w-40"
          type="number"
          inputMode="decimal"
          min="0.01"
          max="1000000"
          step="0.01"
          placeholder="0.00"
          required
          value={price}
          onChange={(e) => setPrice(e.target.value)}
        />
        <Button type="submit" loading={submitting} className="sm:w-28">
          Add
        </Button>
      </div>
      {error && <Alert>{error}</Alert>}
    </form>
  );
}

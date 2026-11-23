"use client";

import { Fragment, useState, type FormEvent } from "react";
import { Button, Input } from "@/components/ui";
import { formatDate, formatPrice, parsePrice } from "@/lib/format";
import type { Product, ProductInput } from "@/lib/types";

interface ProductRowProps {
  product: Product;
  index: number;
  canManage: boolean;
  onUpdate: (id: string, input: ProductInput) => Promise<void>;
  onDelete: (product: Product) => Promise<void>;
}

export function ProductRow({ product, index, canManage, onUpdate, onDelete }: ProductRowProps) {
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(product.name);
  const [price, setPrice] = useState(String(product.price));
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<"save" | "delete" | null>(null);

  function startEditing() {
    setName(product.name);
    setPrice(String(product.price));
    setError(null);
    setEditing(true);
  }

  async function save(event: FormEvent) {
    event.preventDefault();
    const parsedPrice = parsePrice(price);
    if (!name.trim()) return setError("Name is required.");
    if (parsedPrice === null) return setError("Price must be between 0.01 and 1,000,000.");

    setBusy("save");
    setError(null);
    try {
      await onUpdate(product.id, { name: name.trim(), price: parsedPrice });
      setEditing(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save.");
    } finally {
      setBusy(null);
    }
  }

  async function remove() {
    if (!window.confirm(`Delete "${product.name}"?`)) return;
    setBusy("delete");
    setError(null);
    try {
      await onDelete(product);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not delete.");
      setBusy(null);
    }
  }

  const formId = `edit-${product.id}`;
  const cell = "px-4 py-3 align-middle";

  return (
    <>
      <tr className="border-t border-zinc-100 dark:border-zinc-800">
        <td className={`${cell} w-12 text-zinc-400 tabular-nums`}>{index + 1}</td>
        <td className={cell}>
          {editing ? (
            <form id={formId} onSubmit={save}>
              <Input aria-label="Product name" value={name} maxLength={100} required autoFocus
                onChange={(e) => setName(e.target.value)} />
            </form>
          ) : (
            <span className="font-medium">{product.name}</span>
          )}
        </td>
        <td className={`${cell} text-right tabular-nums`}>
          {editing ? (
            <Input form={formId} aria-label="Price" type="number" inputMode="decimal" min="0.01" max="1000000"
              step="0.01" required className="ml-auto w-32 text-right" value={price}
              onChange={(e) => setPrice(e.target.value)} />
          ) : (
            formatPrice(product.price)
          )}
        </td>
        <td className={`${cell} hidden text-sm text-zinc-500 md:table-cell`}>
          {formatDate(product.modifiedAt ?? product.createdAt)}
          {product.modifiedAt && <span className="ml-1 text-xs text-zinc-400">(edited)</span>}
        </td>
        {canManage && (
          <td className={`${cell} text-right`}>
            <div className="flex justify-end gap-2">
              {editing ? (
                <Fragment key="editing">
                  <Button type="submit" form={formId} loading={busy === "save"}>Save</Button>
                  <Button variant="ghost" onClick={() => setEditing(false)} disabled={busy !== null}>Cancel</Button>
                </Fragment>
              ) : (
                <Fragment key="viewing">
                  <Button variant="secondary" onClick={startEditing} disabled={busy !== null}>Edit</Button>
                  <Button variant="danger" onClick={remove} loading={busy === "delete"}>Delete</Button>
                </Fragment>
              )}
            </div>
          </td>
        )}
      </tr>
      {error && (
        <tr>
          <td colSpan={canManage ? 5 : 4} className="px-4 pb-3 text-sm text-red-600 dark:text-red-400">
            {error}
          </td>
        </tr>
      )}
    </>
  );
}

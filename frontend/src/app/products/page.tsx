"use client";

import { useCallback, useEffect, useState } from "react";
import { AddProductForm } from "@/components/products/AddProductForm";
import { ProductTable } from "@/components/products/ProductTable";
import { Alert, Button, Card, FullPageLoader, Spinner } from "@/components/ui";
import { api, ApiError } from "@/lib/api";
import { clearSession, isAdmin, type Session } from "@/lib/session";
import type { Product, ProductInput } from "@/lib/types";
import { useRequireAuth } from "@/lib/use-redirect";

export default function ProductsPage() {
  const session = useRequireAuth();

  if (!session) return <FullPageLoader />;

  return <ProductsView session={session} />;
}

function ProductsView({ session }: { session: Session }) {
  const token = session.accessToken;
  const admin = isAdmin(session);

  const [products, setProducts] = useState<Product[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  // Expired / invalid token -> drop the session; useRequireAuth sends us to /login.
  const rethrow = useCallback((err: unknown): never => {
    if (err instanceof ApiError && err.status === 401) clearSession();
    throw err;
  }, []);

  useEffect(() => {
    let cancelled = false;
    api
      .getProducts(token)
      .then((list) => {
        if (cancelled) return;
        setProducts(list);
        setLoadError(null);
      })
      .catch((err) => {
        if (cancelled) return;
        if (err instanceof ApiError && err.status === 401) clearSession();
        setLoadError(err instanceof Error ? err.message : "Could not load products.");
      });
    return () => {
      cancelled = true;
    };
  }, [token, reloadKey]);

  const addProduct = useCallback(
    async (input: ProductInput) => {
      const created = await api.createProduct(token, input).catch(rethrow);
      setProducts((list) => [created, ...(list ?? [])]);
    },
    [token, rethrow],
  );

  const updateProduct = useCallback(
    async (id: string, input: ProductInput) => {
      const updated = await api.updateProduct(token, id, input).catch(rethrow);
      setProducts((list) => list?.map((p) => (p.id === id ? updated : p)) ?? null);
    },
    [token, rethrow],
  );

  const deleteProduct = useCallback(
    async (product: Product) => {
      await api.deleteProduct(token, product.id).catch((err) => {
        // Already gone on the server: just remove it from the table.
        if (err instanceof ApiError && err.status === 404) return;
        rethrow(err);
      });
      setProducts((list) => list?.filter((p) => p.id !== product.id) ?? null);
    },
    [token, rethrow],
  );

  return (
    <main className="mx-auto w-full max-w-5xl flex-1 space-y-6 px-4 py-8 sm:py-10">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Products</h1>
          <p className="mt-1 text-sm text-zinc-500">
            {admin
              ? "You are an admin: add, edit and delete products."
              : "You can view products. Only admins can add or change them."}
          </p>
        </div>
        {products && (
          <span className="text-sm text-zinc-500">
            {products.length} {products.length === 1 ? "product" : "products"}
          </span>
        )}
      </div>

      {admin && (
        <Card className="p-5">
          <h2 className="mb-3 text-sm font-semibold">Add a product</h2>
          <AddProductForm onAdd={addProduct} />
        </Card>
      )}

      <Card className="overflow-hidden">
        {loadError ? (
          <div className="space-y-3 p-5">
            <Alert>{loadError}</Alert>
            <Button variant="secondary" onClick={() => setReloadKey((k) => k + 1)}>
              Try again
            </Button>
          </div>
        ) : products === null ? (
          <div className="flex items-center justify-center gap-2 py-16 text-sm text-zinc-500">
            <Spinner className="size-5" /> Loading products…
          </div>
        ) : (
          <ProductTable
            products={products}
            canManage={admin}
            onUpdate={updateProduct}
            onDelete={deleteProduct}
          />
        )}
      </Card>
    </main>
  );
}

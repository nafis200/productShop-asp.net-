"use client";

import type { Product, ProductInput } from "@/lib/types";
import { ProductRow } from "./ProductRow";

interface ProductTableProps {
  products: Product[];
  canManage: boolean;
  onUpdate: (id: string, input: ProductInput) => Promise<void>;
  onDelete: (product: Product) => Promise<void>;
}

export function ProductTable({ products, canManage, onUpdate, onDelete }: ProductTableProps) {
  if (products.length === 0) {
    return (
      <div className="px-6 py-16 text-center">
        <p className="font-medium">No products yet</p>
        <p className="mt-1 text-sm text-zinc-500">
          {canManage ? "Add your first product with the form above." : "An admin hasn't added any products yet."}
        </p>
      </div>
    );
  }

  const head = "px-4 py-3 text-xs font-semibold uppercase tracking-wide text-zinc-500";

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead className="bg-zinc-50 dark:bg-zinc-900/60">
          <tr>
            <th className={`${head} text-left`}>#</th>
            <th className={`${head} text-left`}>Name</th>
            <th className={`${head} text-right`}>Price</th>
            <th className={`${head} hidden text-left md:table-cell`}>Last updated</th>
            {canManage && <th className={`${head} text-right`}>Actions</th>}
          </tr>
        </thead>
        <tbody>
          {products.map((product, index) => (
            <ProductRow
              key={product.id}
              product={product}
              index={index}
              canManage={canManage}
              onUpdate={onUpdate}
              onDelete={onDelete}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}

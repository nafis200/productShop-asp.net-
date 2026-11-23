import type {
  AuthResponse,
  LoginInput,
  Product,
  ProductInput,
  RegisterInput,
} from "./types";

export const API_URL = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000").replace(/\/$/, "");

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

interface RequestOptions {
  method?: "GET" | "POST" | "PUT" | "DELETE";
  body?: unknown;
  token?: string;
}

async function request<T>(path: string, { method = "GET", body, token }: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: "application/json" };
  if (body !== undefined) headers["Content-Type"] = "application/json";
  if (token) headers.Authorization = `Bearer ${token}`;

  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(0, `Cannot reach the API at ${API_URL}. Is the backend running?`);
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readErrorMessage(response));
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

// The API returns either { message } (our own errors) or a ProblemDetails object
// with { title, errors: { Field: ["..."] } } (automatic validation errors).
async function readErrorMessage(response: Response): Promise<string> {
  const fallback: Record<number, string> = {
    401: "Your session has expired. Please log in again.",
    403: "Only admins can do this.",
    404: "Not found. It may have been deleted already.",
    429: "Too many attempts. Please wait a minute and try again.",
  };

  try {
    const data = await response.json();
    if (typeof data?.message === "string") return data.message;
    if (data?.errors && typeof data.errors === "object") {
      const messages = Object.values(data.errors as Record<string, string[]>).flat();
      if (messages.length) return messages.join(" ");
    }
    if (typeof data?.title === "string" && !fallback[response.status]) return data.title;
  } catch {
    // body was empty or not JSON
  }

  return fallback[response.status] ?? `Request failed (${response.status}).`;
}

export const api = {
  register: (input: RegisterInput) =>
    request<AuthResponse>("/api/auth/register", { method: "POST", body: input }),

  login: (input: LoginInput) =>
    request<AuthResponse>("/api/auth/login", { method: "POST", body: input }),

  getProducts: (token: string) => request<Product[]>("/api/products", { token }),

  createProduct: (token: string, input: ProductInput) =>
    request<Product>("/api/products", { method: "POST", body: input, token }),

  updateProduct: (token: string, id: string, input: ProductInput) =>
    request<Product>(`/api/products/${id}`, { method: "PUT", body: input, token }),

  deleteProduct: (token: string, id: string) =>
    request<void>(`/api/products/${id}`, { method: "DELETE", token }),
};

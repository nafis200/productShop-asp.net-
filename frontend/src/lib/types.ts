export type Role = "Admin" | "User";

export interface User {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  roles: Role[];
  createdAt: string;
}

export interface AuthResponse {
  tokenType: "Bearer";
  accessToken: string;
  expiresAt: string;
  user: User;
}

export interface Product {
  id: string;
  name: string;
  price: number;
  createdAt: string;
  modifiedAt: string | null;
}

export interface ProductInput {
  name: string;
  price: number;
}

export interface RegisterInput {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

export interface LoginInput {
  email: string;
  password: string;
}

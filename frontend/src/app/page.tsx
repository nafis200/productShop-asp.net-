import { redirect } from "next/navigation";

// "/" has no content of its own: go to the products page (which sends guests to /login).
export default function Home() {
  redirect("/products");
}

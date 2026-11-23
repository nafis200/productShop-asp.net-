import path from "node:path";
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  cacheComponents: true,
  partialPrefetching: true,
  turbopack: {
    // Pin the root to this folder so a stray lockfile higher up the disk
    // (e.g. /media/.../OS/package-lock.json) isn't picked up as the workspace root.
    root: path.join(__dirname),
  },
};

export default nextConfig;

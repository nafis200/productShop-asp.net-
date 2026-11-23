import fs from "node:fs";
import enhancedResolve from "enhanced-resolve";

// Workaround: this project lives under a folder containing "#" ("PracticeC#").
// Tailwind resolves CSS imports with enhanced-resolve, which escapes "#" as "\0#"
// in the returned path, and Tailwind then fails to read the file ("path must be a
// string ... without null bytes"). Tailwind lets us plug in our own resolver via
// globalThis.__tw_resolve, so resolve the same way and strip the escape.
const cssResolver = enhancedResolve.ResolverFactory.createResolver({
  fileSystem: new enhancedResolve.CachedInputFileSystem(fs, 4000),
  useSyncFileSystemCalls: true,
  extensions: [".css"],
  mainFields: ["style"],
  conditionNames: ["style"],
});

globalThis.__tw_resolve = (id, base) => {
  try {
    const resolved = cssResolver.resolveSync({}, base, id);
    return resolved ? resolved.replace(/\0#/g, "#") : undefined;
  } catch {
    return undefined; // let Tailwind fall back to its own resolver
  }
};

const config = {
  plugins: {
    "@tailwindcss/postcss": {},
  },
};

export default config;

import type { AuthStatus } from "../../../src/shared/types.ts";

type Props = {
  auth: AuthStatus | undefined;
  replayCount: number;
  beatmapCount: number;
  loggingIn: boolean;
  onLogin: () => void;
};

export function TopBar({ auth, replayCount, beatmapCount, loggingIn, onLogin }: Props) {
  const signedIn = Boolean(auth?.hasUserToken);
  const canLogin = Boolean(auth?.hasV2) && !signedIn;

  return (
    <header className="sticky top-0 z-20 border-b border-white/10 bg-zinc-950/85 backdrop-blur">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center gap-3 px-4 py-3 sm:px-6">
        <div className="mr-auto min-w-0">
          <h1 className="truncate text-base font-semibold tracking-tight text-zinc-50">replay-analysis</h1>
        </div>
        <div className="flex flex-wrap items-center gap-2 text-xs text-zinc-400">
          <Stat label="replays" value={replayCount} />
          <Stat label="beatmaps" value={beatmapCount} />
          <Stat label="API" value={auth?.hasV2 ? "ready" : "missing"} ok={auth?.hasV2} />
        </div>
        {signedIn ? (
          <span className="inline-flex items-center gap-2 rounded-full border border-emerald-500/30 bg-emerald-500/10 px-3 py-1.5 text-xs font-medium text-emerald-300">
            <span className="size-1.5 rounded-full bg-emerald-400" />
            Signed in
          </span>
        ) : (
          <button
            type="button"
            onClick={onLogin}
            disabled={!canLogin || loggingIn}
            title={auth && !auth.hasV2 ? `Add OAuth credentials to ${auth.secretsPath}` : undefined}
            className="rounded-full bg-violet-400 px-3.5 py-1.5 text-sm font-semibold text-zinc-950 transition hover:bg-violet-300 disabled:cursor-not-allowed disabled:opacity-40"
          >
            {loggingIn ? "Redirecting…" : "Log in with osu!"}
          </button>
        )}
      </div>
    </header>
  );
}

function Stat({ label, value, ok }: { label: string; value: string | number; ok?: boolean }) {
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full border border-white/10 bg-zinc-900 px-2.5 py-1">
      <span className="uppercase tracking-wide">{label}</span>
      <span className={ok === false ? "font-semibold text-rose-300" : "font-semibold text-zinc-100"}>{value}</span>
    </span>
  );
}

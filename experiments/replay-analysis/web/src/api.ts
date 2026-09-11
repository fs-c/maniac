import type { CatalogResponse, FetchRequest, JobStatus, ReplayInspector } from "../../src/shared/types.ts";

async function api<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, init);
  const body = await res.json() as T & { error?: string };
  if (!res.ok)
    throw new Error(body.error ?? `${res.status} ${url}`);
  return body;
}

export function loadCatalog(): Promise<CatalogResponse> {
  return api("/api/catalog");
}

export function startLogin(returnTo: string): Promise<{ authorizeUrl: string }> {
  return api("/api/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ returnTo }),
  });
}

export function startFetch(req: FetchRequest): Promise<JobStatus> {
  return api("/api/fetch", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
}

export type ReplaySlice = {
  eventOffset?: number;
  eventLimit?: number;
  hitOffset?: number;
  hitLimit?: number;
};

export function loadReplay(file: string, slice: ReplaySlice = {}): Promise<ReplayInspector> {
  const q = new URLSearchParams();
  q.set("offset", String(slice.eventOffset ?? 0));
  q.set("limit", String(slice.eventLimit ?? 400));
  q.set("hitsOffset", String(slice.hitOffset ?? 0));
  q.set("hitsLimit", String(slice.hitLimit ?? 400));
  return api(`/api/replays/${encodeURIComponent(file)}?${q}`);
}

export function followJob(id: string, onLog: (line: string) => void): Promise<JobStatus> {
  return new Promise((resolve, reject) => {
    let settled = false;
    const finish = (err?: Error, job?: JobStatus) => {
      if (settled)
        return;
      settled = true;
      es.close();
      if (err)
        reject(err);
      else
        resolve(job ?? { id, status: "ok", saved: 0, logs: [] });
    };
    const es = new EventSource(`/api/jobs/${id}/stream`);
    es.addEventListener("log", (ev) => onLog((ev as MessageEvent).data));
    es.addEventListener("done", (ev) => {
      try {
        finish(undefined, JSON.parse((ev as MessageEvent).data) as JobStatus);
      } catch {
        finish();
      }
    });
    es.onerror = () => {
      void api<JobStatus>(`/api/jobs/${id}`).then((job) => {
        if (job.status === "error")
          finish(new Error(job.error ?? "fetch failed"), job);
        else if (job.status === "ok")
          finish(undefined, job);
      }).catch((err) => finish(err instanceof Error ? err : new Error(String(err))));
    };
  });
}

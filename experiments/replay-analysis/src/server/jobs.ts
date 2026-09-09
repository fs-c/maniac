import { EventEmitter } from "node:events";
import { randomUUID } from "node:crypto";
import type { JobStatus } from "../shared/types.ts";

export class Job extends EventEmitter {
  readonly id = randomUUID();
  status: JobStatus["status"] = "running";
  saved = 0;
  error?: string;
  readonly logs: string[] = [];

  log(line: string): void {
    this.logs.push(line);
    this.emit("log", line);
  }

  finish(ok: boolean, error?: string): void {
    this.status = ok ? "ok" : "error";
    this.error = error;
    this.emit("done");
  }

  snapshot(): JobStatus {
    return {
      id: this.id,
      status: this.status,
      saved: this.saved,
      error: this.error,
      logs: this.logs.slice(),
    };
  }
}

const jobs = new Map<string, Job>();

export function createJob(): Job {
  const job = new Job();
  jobs.set(job.id, job);
  return job;
}

export function getJob(id: string): Job | undefined {
  return jobs.get(id);
}

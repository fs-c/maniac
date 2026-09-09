import { createServer, type IncomingMessage, type ServerResponse } from "node:http";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname } from "node:path";
import type { AuthStatus } from "../shared/types.ts";
import { secretsPath } from "./paths.ts";

export type OsuSecrets = {
  clientId?: string;
  clientSecret?: string;
  redirectUri?: string;
  refreshToken?: string;
  apiKey?: string;
};

type LoginState = {
  status: AuthStatus["login"];
  authorizeUrl?: string;
  error?: string;
  returnTo?: string;
  server?: ReturnType<typeof createServer>;
};

const defaultReturnTo = "http://127.0.0.1:5173/";

export function safeReturnTo(raw: string | undefined): string {
  if (!raw)
    return defaultReturnTo;
  try {
    const u = new URL(raw);
    if (u.protocol !== "http:" && u.protocol !== "https:")
      return defaultReturnTo;
    if (u.hostname !== "127.0.0.1" && u.hostname !== "localhost")
      return defaultReturnTo;
    return `${u.protocol}//${u.hostname}${u.port ? `:${u.port}` : ""}/`;
  } catch {
    return defaultReturnTo;
  }
}

export class Auth {
  secrets: OsuSecrets;
  accessToken?: string;
  tokenExpiry = 0;
  login: LoginState = { status: "idle" };

  constructor() {
    this.secrets = loadSecrets();
  }

  get hasV2(): boolean {
    return Boolean(this.secrets.clientId && this.secrets.clientSecret);
  }

  get hasUserToken(): boolean {
    return Boolean(this.secrets.refreshToken);
  }

  status(): AuthStatus {
    return {
      hasV2: this.hasV2,
      hasUserToken: this.hasUserToken,
      secretsPath: secretsPath(),
      login: this.login.status,
      loginError: this.login.error,
      authorizeUrl: this.login.authorizeUrl,
    };
  }

  credentialHelp(): string {
    return [
      "No osu! API credentials. Create an OAuth app at https://osu.ppy.sh/home/account/edit#oauth",
      "Callback: http://127.0.0.1:7272/callback",
      `Then set OSU_CLIENT_ID / OSU_CLIENT_SECRET or write ${secretsPath()}`,
      "Replay download needs a user token — use Log in on this page.",
    ].join("\n");
  }

  async startLogin(returnTo?: string): Promise<string> {
    if (!this.hasV2)
      throw new Error(this.credentialHelp());
    this.login.returnTo = safeReturnTo(returnTo);
    if (this.login.status === "waiting" && this.login.authorizeUrl)
      return this.login.authorizeUrl;

    const redirect = this.secrets.redirectUri ?? "http://127.0.0.1:7272/callback";
    const url = new URL(redirect);
    const authorize = new URL("https://osu.ppy.sh/oauth/authorize");
    authorize.searchParams.set("client_id", this.secrets.clientId!);
    authorize.searchParams.set("redirect_uri", redirect);
    authorize.searchParams.set("response_type", "code");
    authorize.searchParams.set("scope", "public");

    await this.listenForCode(url, redirect);
    this.login.status = "waiting";
    this.login.error = undefined;
    this.login.authorizeUrl = authorize.toString();
    return this.login.authorizeUrl;
  }

  async ensureToken(preferUser: boolean): Promise<void> {
    if (this.accessToken && Date.now() < this.tokenExpiry)
      return;
    if (preferUser && this.hasUserToken) {
      await this.exchange({
        grant_type: "refresh_token",
        refresh_token: this.secrets.refreshToken!,
        client_id: this.secrets.clientId!,
        client_secret: this.secrets.clientSecret!,
      }, true);
      return;
    }
    if (!this.hasV2)
      return;
    await this.exchange({
      grant_type: "client_credentials",
      client_id: this.secrets.clientId!,
      client_secret: this.secrets.clientSecret!,
      scope: "public",
    }, false);
  }

  async exchange(form: Record<string, string>, saveRefresh: boolean): Promise<void> {
    const body = new URLSearchParams(form);
    const resp = await fetch("https://osu.ppy.sh/oauth/token", {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded", Accept: "application/json" },
      body,
    });
    const text = await resp.text();
    if (!resp.ok)
      throw new Error(`oauth token: ${resp.status} ${text.slice(0, 180)}`);
    const doc = JSON.parse(text) as { access_token: string; expires_in?: number; refresh_token?: string };
    this.accessToken = doc.access_token;
    const expires = doc.expires_in ?? 3600;
    this.tokenExpiry = Date.now() + Math.max(30, expires - 30) * 1000;
    if (saveRefresh && doc.refresh_token) {
      this.secrets.refreshToken = doc.refresh_token;
      saveSecrets(this.secrets);
    }
  }

  private listenForCode(uri: URL, redirect: string): Promise<void> {
    return new Promise((resolve, reject) => {
      this.login.server?.close();
      const server = createServer((req, res) => {
        void this.handleCallback(req, res, redirect);
      });
      this.login.server = server;
      server.on("error", (err) => {
        this.login = { status: "error", error: err.message };
        reject(err);
      });
      const port = Number(uri.port || (uri.protocol === "https:" ? 443 : 80));
      const host = uri.hostname === "localhost" ? "127.0.0.1" : uri.hostname;
      server.listen(port, host, () => resolve());
    });
  }

  private async handleCallback(req: IncomingMessage, res: ServerResponse, redirect: string): Promise<void> {
    const url = new URL(req.url ?? "/", `http://${req.headers.host ?? "127.0.0.1"}`);
    if (url.pathname === "/favicon.ico") {
      res.writeHead(204);
      res.end();
      return;
    }

    const returnTo = this.login.returnTo ?? defaultReturnTo;
    const code = url.searchParams.get("code");
    const oauthError = url.searchParams.get("error");
    let result: "ok" | "error" = "ok";
    let reason = "";

    try {
      if (oauthError)
        throw new Error(oauthError);
      if (!code)
        throw new Error("OAuth did not return a code.");
      await this.exchange({
        grant_type: "authorization_code",
        code,
        redirect_uri: redirect,
        client_id: this.secrets.clientId!,
        client_secret: this.secrets.clientSecret!,
      }, true);
      this.login.status = "ok";
      this.login.error = undefined;
      this.login.authorizeUrl = undefined;
    } catch (err) {
      reason = err instanceof Error ? err.message : String(err);
      this.login.status = "error";
      this.login.error = reason;
      result = "error";
    }

    const dest = new URL(returnTo);
    dest.searchParams.set("login", result);
    if (reason)
      dest.searchParams.set("reason", reason.slice(0, 160));
    res.writeHead(302, { Location: dest.toString() });
    res.end();
    this.login.server?.close();
    this.login.server = undefined;
  }
}

function env(name: string): string | undefined {
  const v = process.env[name];
  return v && v.trim() ? v.trim() : undefined;
}

function loadSecrets(): OsuSecrets {
  let s: OsuSecrets = {};
  const candidates = [
    secretsPath(),
    env("OSU_OAUTH_PATH") ?? "",
  ].filter(Boolean);
  for (const path of candidates) {
    if (!existsSync(path))
      continue;
    try {
      s = JSON.parse(readFileSync(path, "utf8")) as OsuSecrets;
      // file may be snake_case from the C# experiment
      const raw = s as OsuSecrets & {
        client_id?: string;
        client_secret?: string;
        redirect_uri?: string;
        refresh_token?: string;
        api_key?: string;
      };
      s = {
        clientId: raw.clientId ?? raw.client_id,
        clientSecret: raw.clientSecret ?? raw.client_secret,
        redirectUri: raw.redirectUri ?? raw.redirect_uri,
        refreshToken: raw.refreshToken ?? raw.refresh_token,
        apiKey: raw.apiKey ?? raw.api_key,
      };
      break;
    } catch {
      // keep defaults
    }
  }
  s.clientId = env("OSU_CLIENT_ID") ?? s.clientId;
  s.clientSecret = env("OSU_CLIENT_SECRET") ?? s.clientSecret;
  s.refreshToken = env("OSU_REFRESH_TOKEN") ?? s.refreshToken;
  s.apiKey = env("OSU_API_KEY") ?? s.apiKey;
  s.redirectUri = env("OSU_REDIRECT_URI") ?? s.redirectUri ?? "http://127.0.0.1:7272/callback";
  return s;
}

function saveSecrets(secrets: OsuSecrets): void {
  const path = secretsPath();
  mkdirSync(dirname(path), { recursive: true });
  const existing = existsSync(path) ? JSON.parse(readFileSync(path, "utf8")) as Record<string, unknown> : {};
  const body = {
    ...existing,
    client_id: secrets.clientId,
    client_secret: secrets.clientSecret,
    redirect_uri: secrets.redirectUri,
    refresh_token: secrets.refreshToken,
    api_key: secrets.apiKey,
  };
  writeFileSync(path, JSON.stringify(body, null, 2));
}

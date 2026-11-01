import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, firstValueFrom, map, of, shareReplay } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, Session, SignUpResponse, UserSummary } from '../api/api.models';

/** Refresh this long before the access token expires, so requests never go out with a dead token. */
const REFRESH_AHEAD_MS = 60_000;

type TabMessage = { type: 'logout' } | { type: 'login' };

/**
 * Token handling:
 *  - Access token (15 min): kept in memory only. Never written to localStorage, so a script
 *    injected into the page (XSS) has no long-lived token to steal.
 *  - Refresh token (7 days): an httpOnly cookie set by the server. The browser sends it to
 *    /api/auth automatically; this code can't read it at all.
 * Because memory is wiped on reload, the app "silently refreshes" on startup: if the cookie is
 * still valid, the user is signed in again without typing a password.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/auth`;

  // withCredentials: lets the browser store/send the cookie on cross-origin calls (4200 → 5000).
  private readonly cookieRequest = { withCredentials: true };

  private readonly session = signal<Session | null>(null);
  private refreshInFlight$: Observable<Session> | null = null;
  private refreshTimer: ReturnType<typeof setTimeout> | undefined;

  // Tabs share the cookie but not memory. A channel tells the other tabs about sign-in/out.
  private readonly channel = typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel('ytube-auth') : null;

  readonly user = computed<UserSummary | null>(() => this.session()?.user ?? null);
  readonly isLoggedIn = computed(() => this.session() !== null);

  constructor() {
    localStorage.removeItem('ytube.auth'); // tokens used to live here; clean up old copies

    this.channel?.addEventListener('message', (event: MessageEvent<TabMessage>) => {
      if (event.data.type === 'logout') this.endSession(false);
      if (event.data.type === 'login' && !this.isLoggedIn()) this.restoreSession();
    });
  }

  get accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  /** Called once at startup (app initializer). Never fails: no valid cookie just means "guest". */
  restoreSession(): Promise<unknown> {
    return firstValueFrom(this.refresh().pipe(catchError(() => of(null))));
  }

  signUp(email: string, password: string): Observable<SignUpResponse> {
    return this.http
      .post<ApiResponse<SignUpResponse>>(`${this.baseUrl}/signup`, { email, password })
      .pipe(map((res) => res.data!));
  }

  verifyOtp(signupId: string, otp: string): Observable<Session> {
    return this.http
      .post<ApiResponse<Session>>(`${this.baseUrl}/verify-otp`, { signupId, otp }, this.cookieRequest)
      .pipe(map((res) => this.startSession(res.data!, true)));
  }

  resendOtp(signupId: string): Observable<SignUpResponse> {
    return this.http
      .post<ApiResponse<SignUpResponse>>(`${this.baseUrl}/resend-otp`, { signupId })
      .pipe(map((res) => res.data!));
  }

  login(email: string, password: string): Observable<Session> {
    return this.http
      .post<ApiResponse<Session>>(`${this.baseUrl}/login`, { email, password }, this.cookieRequest)
      .pipe(map((res) => this.startSession(res.data!, true)));
  }

  /** Several callers (timer, interceptor, startup) can ask at once; they share one request. */
  refresh(): Observable<Session> {
    this.refreshInFlight$ ??= this.http.post<ApiResponse<Session>>(`${this.baseUrl}/refresh`, null, this.cookieRequest).pipe(
      map((res) => this.startSession(res.data!, false)),
      finalize(() => (this.refreshInFlight$ = null)),
      shareReplay(1),
    );
    return this.refreshInFlight$;
  }

  logout(): void {
    this.http.post(`${this.baseUrl}/logout`, null, this.cookieRequest).subscribe({ error: () => {} });
    this.endSession(true);
  }

  /**
   * Session is over (logout, or refresh token rejected). No redirect: every page works for guests
   * and login-only pages switch to their "Sign in" state on their own.
   */
  endSession(broadcast = true): void {
    clearTimeout(this.refreshTimer);
    this.session.set(null);
    if (broadcast) this.channel?.postMessage({ type: 'logout' } satisfies TabMessage);
  }

  /** True when the access token is expired or about to be. */
  accessTokenExpiringSoon(): boolean {
    const expires = this.session()?.accessTokenExpiresAtUtc;
    return !!expires && new Date(expires).getTime() - Date.now() < 30_000;
  }

  private startSession(session: Session, isNewLogin: boolean): Session {
    this.session.set(session);
    this.scheduleRefresh(session);
    if (isNewLogin) this.channel?.postMessage({ type: 'login' } satisfies TabMessage);
    return session;
  }

  /** Proactive refresh: renew shortly before expiry instead of waiting for a 401. */
  private scheduleRefresh(session: Session): void {
    clearTimeout(this.refreshTimer);
    const delay = new Date(session.accessTokenExpiresAtUtc).getTime() - Date.now() - REFRESH_AHEAD_MS;
    this.refreshTimer = setTimeout(
      () => this.refresh().subscribe({ error: () => this.endSession(false) }),
      Math.max(delay, 5_000),
    );
  }
}

import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const withToken = (req: HttpRequest<unknown>, token: string | null) =>
  token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

/**
 * Adds the in-memory access token to API calls (public endpoints use it for "likedByMe" etc.).
 * Safety nets on top of the proactive refresh timer (e.g. laptop woke up after sleeping):
 *  - token about to expire → refresh first, then send;
 *  - request still gets 401 → refresh once and retry;
 *  - refresh rejected → the session is over: sign out and continue as a guest.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  // Auth endpoints use the cookie, not the bearer token; a 401 from them must not trigger a refresh loop.
  if (req.url.includes('/api/auth/') || !auth.isLoggedIn()) return next(req);

  const send = () =>
    next(withToken(req, auth.accessToken)).pipe(
      catchError((error: unknown) => {
        if (!(error instanceof HttpErrorResponse) || error.status !== 401) return throwError(() => error);

        return auth.refresh().pipe(
          switchMap((session) => next(withToken(req, session.accessToken))),
          catchError((refreshError) => {
            auth.endSession(false);
            return throwError(() => refreshError);
          }),
        );
      }),
    );

  if (!auth.accessTokenExpiringSoon()) return send();

  return auth.refresh().pipe(
    switchMap(() => send()),
    catchError((error) => {
      if (error instanceof HttpErrorResponse && error.url?.includes('/api/auth/refresh')) {
        auth.endSession(false);
        return next(req); // continue as a guest
      }
      return throwError(() => error);
    }),
  );
};

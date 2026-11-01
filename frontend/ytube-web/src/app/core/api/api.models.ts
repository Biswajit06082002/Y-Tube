/** Mirrors YTube.Common.Responses.ApiResponse<T> on the backend. */
export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  message: string | null;
  errorCode: string | null;
}

export interface UserSummary {
  id: string;
  email: string;
}

/** What login/refresh return. The refresh token is NOT here: it lives in an httpOnly cookie. */
export interface Session {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  user: UserSummary;
}

export interface SignUpResponse {
  signupId: string;
  otpExpiresInSeconds: number;
}

/** Pulls the backend's message out of an HttpErrorResponse (falls back to a generic one). */
export function errorMessage(err: unknown): string {
  const body = (err as { error?: Partial<ApiResponse<unknown>> })?.error;
  if (body?.message) return body.message;
  if ((err as { status?: number })?.status === 429) return 'Too many attempts. Please wait a minute and try again.';
  if ((err as { status?: number })?.status === 0) return 'Cannot reach the server. Is the gateway running?';
  return 'Something went wrong. Please try again.';
}

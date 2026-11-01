/**
 * Only allow in-app paths as a return target. Accepting any URL here would be an
 * "open redirect": a phishing link could send users to another site after login.
 */
export function safeReturnUrl(url: string | null | undefined): string {
  return url && url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/login') ? url : '/';
}

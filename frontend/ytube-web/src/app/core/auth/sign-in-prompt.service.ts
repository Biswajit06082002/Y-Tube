import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

export interface SignInPrompt {
  title: string;
  message: string;
  /** Just a notice (OK button) rather than a sign-in request. */
  infoOnly?: boolean;
}

/**
 * The "gate" for login-only actions on public pages (like, subscribe, upload…).
 * Logged in → the action runs. Logged out → a small "Sign in to …" dialog opens instead,
 * and after signing in the user comes back to the same page.
 */
@Injectable({ providedIn: 'root' })
export class SignInPromptService {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly prompt = signal<SignInPrompt | null>(null);

  /** Returns true when the caller may continue; otherwise shows the prompt. */
  require(prompt: SignInPrompt): boolean {
    if (this.auth.isLoggedIn()) return true;
    this.prompt.set(prompt);
    return false;
  }

  dismiss(): void {
    this.prompt.set(null);
  }

  goToSignIn(): void {
    this.prompt.set(null);
    this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
  }
}

import { Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { errorMessage } from '../../../core/api/api.models';
import { AuthService } from '../../../core/auth/auth.service';
import { safeReturnUrl } from '../../../core/auth/return-url';
import { AuthCard } from '../auth-card';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, AuthCard],
  template: `
    <app-auth-card title="Welcome back" subtitle="Sign in to continue to ytube.">
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
        <input formControlName="email" type="email" placeholder="Email" autocomplete="email" class="input" />
        <input formControlName="password" type="password" placeholder="Password" autocomplete="current-password" class="input" />

        @if (error()) {
          <p class="rounded-xl bg-red-500/10 px-3 py-2 text-sm text-red-500">{{ error() }}</p>
        }

        <button type="submit" [disabled]="form.invalid || loading()" class="btn-primary w-full">
          {{ loading() ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>

      <p class="mt-8 text-center text-sm text-muted">
        New here? <a routerLink="/signup" [queryParams]="{ returnUrl: returnUrl() }" class="font-semibold text-accent hover:underline">Create an account</a>
      </p>
    </app-auth-card>
  `,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** ?returnUrl= — where to go after signing in. */
  readonly returnUrl = input<string | null>(null);

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);

    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => this.router.navigateByUrl(safeReturnUrl(this.returnUrl())),
      error: (err) => {
        this.error.set(errorMessage(err));
        this.loading.set(false);
      },
    });
  }
}

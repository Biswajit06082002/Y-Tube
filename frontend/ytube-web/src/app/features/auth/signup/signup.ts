import { Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { errorMessage } from '../../../core/api/api.models';
import { AuthService } from '../../../core/auth/auth.service';
import { AuthCard } from '../auth-card';

@Component({
  selector: 'app-signup',
  imports: [ReactiveFormsModule, RouterLink, AuthCard],
  template: `
    <app-auth-card title="Create your account" subtitle="We'll email you a 6-digit code">
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
        <input formControlName="email" type="email" placeholder="Email" autocomplete="email" class="input" />
        <input formControlName="password" type="password" placeholder="Password (min 8 characters)" autocomplete="new-password" class="input" />

        @if (error()) {
          <p class="rounded-xl bg-red-500/10 px-3 py-2 text-sm text-red-500">{{ error() }}</p>
        }

        <button type="submit" [disabled]="form.invalid || loading()" class="btn-primary w-full">
          {{ loading() ? 'Sending code…' : 'Continue' }}
        </button>
      </form>

      <p class="mt-8 text-center text-sm text-muted">
        Already have an account? <a routerLink="/login" [queryParams]="{ returnUrl: returnUrl() }" class="font-semibold text-accent hover:underline">Sign in</a>
      </p>
    </app-auth-card>
  `,
})
export class Signup {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly returnUrl = input<string | null>(null);

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  submit(): void {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);

    const { email, password } = this.form.getRawValue();
    this.auth.signUp(email, password).subscribe({
      next: (res) => this.router.navigate(['/verify', res.signupId], { state: { email }, queryParams: { returnUrl: this.returnUrl() } }),
      error: (err) => {
        this.error.set(errorMessage(err));
        this.loading.set(false);
      },
    });
  }
}

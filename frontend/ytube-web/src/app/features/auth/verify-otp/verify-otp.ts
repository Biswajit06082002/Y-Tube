import { Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { errorMessage } from '../../../core/api/api.models';
import { AuthService } from '../../../core/auth/auth.service';
import { safeReturnUrl } from '../../../core/auth/return-url';
import { AuthCard } from '../auth-card';

@Component({
  selector: 'app-verify-otp',
  imports: [ReactiveFormsModule, RouterLink, AuthCard],
  template: `
    <app-auth-card title="Verify your email" [subtitle]="subtitle">
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
        <input
          formControlName="otp"
          inputmode="numeric"
          maxlength="6"
          placeholder="123456"
          autocomplete="one-time-code"
          class="input text-center text-2xl tracking-[0.5em]"
        />

        @if (error()) {
          <p class="rounded-xl bg-red-500/10 px-3 py-2 text-sm text-red-500">{{ error() }}</p>
        }
        @if (info()) {
          <p class="rounded-xl bg-emerald-500/10 px-3 py-2 text-sm text-emerald-500">{{ info() }}</p>
        }

        <button type="submit" [disabled]="form.invalid || loading()" class="btn-primary w-full">
          {{ loading() ? 'Verifying…' : 'Verify' }}
        </button>
      </form>

      <div class="mt-6 flex justify-between text-sm">
        <button type="button" (click)="resend()" [disabled]="loading()" class="font-semibold text-accent hover:underline disabled:opacity-50">
          Resend code
        </button>
        <a routerLink="/signup" class="text-muted hover:underline">Start over</a>
      </div>
    </app-auth-card>
  `,
})
export class VerifyOtp {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Bound from the :signupId route param (withComponentInputBinding). */
  readonly signupId = input.required<string>();
  readonly returnUrl = input<string | null>(null);

  protected readonly subtitle = this.emailHint();
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly info = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    otp: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
  });

  submit(): void {
    if (this.form.invalid) return;
    this.start();

    this.auth.verifyOtp(this.signupId(), this.form.getRawValue().otp).subscribe({
      next: () => this.router.navigateByUrl(safeReturnUrl(this.returnUrl())),
      error: (err) => this.fail(err),
    });
  }

  resend(): void {
    this.start();
    this.auth.resendOtp(this.signupId()).subscribe({
      next: () => {
        this.info.set('A new code is on its way.');
        this.loading.set(false);
      },
      error: (err) => this.fail(err),
    });
  }

  private start(): void {
    this.loading.set(true);
    this.error.set(null);
    this.info.set(null);
  }

  private fail(err: unknown): void {
    this.error.set(errorMessage(err));
    this.loading.set(false);
  }

  private emailHint(): string {
    const email = this.router.currentNavigation()?.extras.state?.['email'] as string | undefined;
    return email ? `Enter the code we sent to ${email}` : 'Enter the 6-digit code from your email';
  }
}

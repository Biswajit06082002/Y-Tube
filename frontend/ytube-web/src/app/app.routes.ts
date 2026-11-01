import { Routes } from '@angular/router';
import { guestGuard } from './core/auth/auth.guards';
import { Shell } from './layout/shell';

/**
 * Browsing is public: no route inside the shell needs a guard. Login-only *pages* render a
 * sign-in prompt for guests, and login-only *actions* go through SignInPromptService.
 * authGuard (redirect to /login) stays available for pages that make no sense signed out, e.g. upload.
 */
export const routes: Routes = [
  {
    path: '',
    component: Shell,
    children: [
      { path: '', loadComponent: () => import('./features/home/home').then((m) => m.Home) },
      { path: 'watch/:id', loadComponent: () => import('./features/watch/watch').then((m) => m.Watch) },
      { path: 'feed/subscriptions', loadComponent: () => import('./features/library/library-pages').then((m) => m.Subscriptions) },
      { path: 'feed/history', loadComponent: () => import('./features/library/library-pages').then((m) => m.History) },
      { path: 'feed/liked', loadComponent: () => import('./features/library/library-pages').then((m) => m.Liked) },
    ],
  },
  { path: 'login', canActivate: [guestGuard], loadComponent: () => import('./features/auth/login/login').then((m) => m.Login) },
  { path: 'signup', canActivate: [guestGuard], loadComponent: () => import('./features/auth/signup/signup').then((m) => m.Signup) },
  {
    path: 'verify/:signupId',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/verify-otp/verify-otp').then((m) => m.VerifyOtp),
  },
  { path: '**', redirectTo: '' },
];

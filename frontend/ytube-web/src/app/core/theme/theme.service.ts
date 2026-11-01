import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../api/api.models';
import { AuthService } from '../auth/auth.service';

export type ThemeId = 'daylight' | 'midnight' | 'ember' | 'lagoon' | 'sakura';
export type ThemePreference = ThemeId | 'system';

export interface ThemeOption {
  id: ThemePreference;
  name: string;
  /** Swatch colors for the picker: [background, accent, accent-2]. */
  swatch: [string, string, string];
}

export const THEMES: ThemeOption[] = [
  { id: 'system', name: 'System', swatch: ['linear-gradient(135deg,#f4f3fa 50%,#050409 50%)', '#6d4aff', '#ff4fa3'] },
  { id: 'daylight', name: 'Daylight', swatch: ['#f4f3fa', '#6d4aff', '#ff4fa3'] },
  { id: 'midnight', name: 'Midnight', swatch: ['#050409', '#8d6dff', '#ff5cad'] },
  { id: 'ember', name: 'Ember', swatch: ['#080504', '#ff7a3d', '#ffc23d'] },
  { id: 'lagoon', name: 'Lagoon', swatch: ['#02080a', '#2dd4bf', '#60a5fa'] },
  { id: 'sakura', name: 'Sakura', swatch: ['#fff5f7', '#e11d74', '#f59e0b'] },
];

/** Same key the inline script in index.html reads, so the theme is applied before Angular even starts. */
const STORAGE_KEY = 'ytube.theme';

interface PreferencesDto {
  theme: ThemePreference;
  isDefault: boolean;
}

/**
 * Where the theme preference lives:
 *  - Guests: browser only (localStorage).
 *  - Signed in: the User service (SQL + Redis cache) is the source of truth, so the choice follows
 *    the user to any device; localStorage stays as a fast local copy to avoid a flash on load.
 * On sign-in: a saved server preference wins; if the account has none yet, the guest's local
 * choice is uploaded so it isn't lost.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly url = `${environment.apiBaseUrl}/api/users/me/preferences`;

  private readonly systemDark = signal(matchMedia('(prefers-color-scheme: dark)').matches);

  readonly preference = signal<ThemePreference>(this.readLocal());
  readonly active = computed<ThemeId>(() => {
    const pref = this.preference();
    return pref === 'system' ? (this.systemDark() ? 'midnight' : 'daylight') : pref;
  });

  constructor() {
    // Follow the OS when the preference is "system".
    const media = matchMedia('(prefers-color-scheme: dark)');
    const onChange = (e: MediaQueryListEvent) => this.systemDark.set(e.matches);
    media.addEventListener('change', onChange);
    inject(DestroyRef).onDestroy(() => media.removeEventListener('change', onChange));

    // Apply + remember locally whenever it changes.
    effect(() => {
      document.documentElement.dataset['theme'] = this.active();
      try {
        localStorage.setItem(STORAGE_KEY, this.preference());
      } catch {
        /* storage blocked (private mode): theme still works for this visit */
      }
    });

    // Each time a (different) user signs in, sync with their saved preference.
    // A computed string only changes on sign-in/out, not on every token refresh.
    const userId = computed(() => this.auth.user()?.id ?? null);
    effect(() => {
      if (userId()) untracked(() => this.syncFromServer());
    });
  }

  set(theme: ThemePreference): void {
    this.preference.set(theme);
    if (this.auth.isLoggedIn()) {
      this.http.put<ApiResponse<PreferencesDto>>(this.url, { theme }).subscribe({ error: () => {} });
    }
  }

  private syncFromServer(): void {
    this.http.get<ApiResponse<PreferencesDto>>(this.url).subscribe({
      next: (res) => {
        const server = res.data!;
        if (!server.isDefault) {
          this.preference.set(server.theme);
        } else if (this.preference() !== 'system') {
          this.set(this.preference()); // first sign-in: keep what they picked as a guest
        }
      },
      error: () => {}, // offline / service down: keep the local theme
    });
  }

  private readLocal(): ThemePreference {
    try {
      const value = localStorage.getItem(STORAGE_KEY) as ThemePreference | null;
      return value && THEMES.some((t) => t.id === value) ? value : 'system';
    } catch {
      return 'system';
    }
  }
}

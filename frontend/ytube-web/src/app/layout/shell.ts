import { Component, HostListener, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { SignInPromptService } from '../core/auth/sign-in-prompt.service';
import { ChannelAvatar } from '../shared/channel-avatar';
import { Icon, IconName } from '../shared/icon';
import { Logo } from '../shared/logo';
import { ThemePicker } from '../shared/theme-picker';

interface NavItem {
  label: string;
  icon: IconName;
  link: string;
}

/** App frame: floating sidebar panel + frosted top bar around every browsing page. */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule, Icon, ChannelAvatar, Logo, ThemePicker],
  template: `
    <!-- Mobile backdrop for the drawer -->
    @if (drawerOpen()) {
      <div class="fixed inset-0 z-30 bg-black/50 backdrop-blur-sm lg:hidden" (click)="drawerOpen.set(false)"></div>
    }

    <!-- Sidebar: floating panel on desktop (wide or compact), slide-in drawer on mobile -->
    <aside
      class="panel fixed top-3 bottom-3 left-3 z-40 flex flex-col overflow-y-auto p-3 transition-all duration-300"
      [class]="sidebarClasses()"
    >
      <div class="mb-6 flex items-center justify-between px-1 pt-1">
        <app-logo [showWordmark]="!compact()" />
        @if (!compact()) {
          <button (click)="toggleSidebar()" class="icon-btn hidden lg:inline-flex" aria-label="Collapse sidebar">
            <app-icon name="collapse" [size]="20" />
          </button>
        }
      </div>

      @if (compact()) {
        <button (click)="toggleSidebar()" class="icon-btn mx-auto mb-2 rotate-180" aria-label="Expand sidebar">
          <app-icon name="collapse" [size]="20" />
        </button>
      }

      <nav class="space-y-1">
        @for (item of nav; track item.link) {
          <a
            [routerLink]="item.link"
            routerLinkActive="active"
            [routerLinkActiveOptions]="{ exact: true }"
            (click)="drawerOpen.set(false)"
            class="nav-item"
            [class.justify-center]="compact()"
            [class.px-0]="compact()"
            [attr.title]="compact() ? item.label : null"
          >
            <app-icon [name]="item.icon" [size]="22" />
            @if (!compact()) {
              <span>{{ item.label }}</span>
            }
          </a>
        }
      </nav>

      @if (!compact()) {
        <div class="mt-auto pt-6">
          @if (!auth.isLoggedIn()) {
            <div class="brand-gradient relative overflow-hidden rounded-2xl p-4 text-white">
              <app-icon name="sparkle" [size]="28" />
              <p class="mt-2 font-display text-lg leading-tight font-semibold">Make it yours</p>
              <p class="mt-1 text-sm text-white/85">Sign in to like videos, follow channels and keep your theme on every device.</p>
              <a
                [routerLink]="['/login']"
                [queryParams]="{ returnUrl: router.url }"
                class="mt-3 inline-flex rounded-xl bg-white px-4 py-2 text-sm font-semibold text-neutral-900 hover:bg-white/90"
              >
                Sign in
              </a>
            </div>
          } @else {
            <p class="px-4 text-xs text-muted">YTube · learning build</p>
          }
        </div>
      }
    </aside>

    <div class="transition-[padding] duration-300" [class]="compact() ? 'lg:pl-[104px]' : 'lg:pl-[288px]'">
      <!-- Top bar -->
      <header class="sticky top-0 z-20 h-[76px] px-4 lg:px-6">
        <!-- Soft blur that fades in once scrolled (no hard strip across the background) -->
        <div class="top-fade" [class.visible]="scrolled()"></div>

        <div class="relative flex h-full items-center gap-3">
        <button (click)="drawerOpen.set(true)" class="icon-btn lg:hidden" aria-label="Open menu">
          <app-icon name="menu" />
        </button>
        <div class="lg:hidden"><app-logo [showWordmark]="false" /></div>

        <form (ngSubmit)="search()" class="mx-auto flex w-full max-w-xl items-center">
          <label class="glass-strong flex h-12 w-full items-center gap-3 rounded-2xl px-4 transition focus-within:!border-accent/60 focus-within:ring-4 focus-within:ring-accent/15">
            <app-icon name="search" [size]="20" class="text-muted" />
            <input [(ngModel)]="query" name="q" placeholder="Search videos and channels" class="w-full bg-transparent text-sm outline-none placeholder:text-muted" />
          </label>
        </form>

        <!-- Actions cluster: one floating glass pill -->
        <div class="glass-strong flex h-12 items-center gap-1 rounded-2xl px-1">
          <!-- Theme popover (available to guests too) -->
          <div class="relative">
            <button (click)="openMenu('theme')" class="icon-btn" aria-label="Change theme" title="Theme">
              <app-icon name="palette" />
            </button>
            @if (menu() === 'theme') {
              <div class="fixed inset-0 z-40" (click)="menu.set(null)"></div>
              <div class="glass-strong absolute right-0 z-50 mt-2 w-80 rounded-3xl p-4">
                <app-theme-picker
                  [hint]="auth.isLoggedIn() ? 'Saved to your account — follows you to any device.' : 'Saved in this browser. Sign in to keep it on every device.'"
                />
              </div>
            }
          </div>

          @if (auth.user(); as user) {
            <button (click)="upload()" class="icon-btn" title="Upload" aria-label="Upload">
              <app-icon name="upload" />
            </button>
            <div class="relative">
              <button (click)="openMenu('account')" class="flex h-10 w-10 items-center justify-center rounded-xl transition hover:bg-fg/8" aria-label="Account menu">
                <app-channel-avatar [name]="user.email" [size]="32" />
              </button>
              @if (menu() === 'account') {
                <div class="fixed inset-0 z-40" (click)="menu.set(null)"></div>
                <div class="glass-strong absolute right-0 z-50 mt-2 w-72 overflow-hidden rounded-3xl p-2">
                  <div class="flex items-center gap-3 p-3">
                    <app-channel-avatar [name]="user.email" [size]="40" />
                    <div class="min-w-0">
                      <p class="truncate text-sm font-semibold">{{ user.email }}</p>
                      <p class="text-xs text-muted">Signed in</p>
                    </div>
                  </div>
                  <hr class="my-1 border-line" />
                  <button (click)="signOut()" class="nav-item w-full">
                    <app-icon name="logout" [size]="20" /> Sign out
                  </button>
                </div>
              }
            </div>
          } @else {
            <a [routerLink]="['/login']" [queryParams]="{ returnUrl: router.url }" class="btn-primary !rounded-xl !py-2">
              Sign in
            </a>
          }
        </div>
        </div>
      </header>

      <main class="px-4 pb-12 lg:px-6">
        <router-outlet />
      </main>
    </div>

    <!-- "Sign in to …" dialog for login-only actions -->
    @if (prompt.prompt(); as p) {
      <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/30 p-4 backdrop-blur-md" (click)="prompt.dismiss()">
        <div class="glass-strong w-full max-w-sm rounded-3xl p-6" (click)="$event.stopPropagation()" role="dialog" aria-modal="true">
          <div class="brand-gradient mb-4 flex h-12 w-12 items-center justify-center rounded-2xl text-white">
            <app-icon [name]="p.infoOnly ? 'sparkle' : 'account'" [size]="26" />
          </div>
          <h2 class="font-display text-xl font-semibold">{{ p.title }}</h2>
          <p class="mt-1.5 text-sm text-muted">{{ p.message }}</p>
          <div class="mt-6 flex justify-end gap-2">
            @if (p.infoOnly) {
              <button (click)="prompt.dismiss()" class="btn-primary">Got it</button>
            } @else {
              <button (click)="prompt.dismiss()" class="btn-soft">Not now</button>
              <button (click)="prompt.goToSignIn()" class="btn-primary">Sign in</button>
            }
          </div>
        </div>
      </div>
    }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly prompt = inject(SignInPromptService);
  protected readonly router = inject(Router);

  /** Desktop: wide or compact sidebar. Mobile: hidden drawer. */
  protected readonly compact = signal(false);
  protected readonly drawerOpen = signal(false);
  protected readonly menu = signal<'theme' | 'account' | null>(null);
  protected readonly scrolled = signal(false);
  protected query = '';

  protected readonly nav: NavItem[] = [
    { label: 'Discover', icon: 'home', link: '/' },
    { label: 'Following', icon: 'subscriptions', link: '/feed/subscriptions' },
    { label: 'History', icon: 'history', link: '/feed/history' },
    { label: 'Liked', icon: 'like', link: '/feed/liked' },
  ];

  @HostListener('window:scroll')
  onScroll(): void {
    this.scrolled.set(window.scrollY > 8);
  }

  protected sidebarClasses(): string {
    const width = this.compact() ? 'w-[80px]' : 'w-64';
    const visibility = this.drawerOpen() ? 'translate-x-0' : '-translate-x-[110%] lg:translate-x-0';
    return `${width} ${visibility}`;
  }

  toggleSidebar(): void {
    this.compact.update((c) => !c);
  }

  openMenu(name: 'theme' | 'account'): void {
    this.menu.update((current) => (current === name ? null : name));
  }

  search(): void {
    const q = this.query.trim();
    this.router.navigate(['/'], { queryParams: q ? { q } : {} });
  }

  upload(): void {
    this.prompt.prompt.set({
      title: 'Uploads are coming soon',
      message: 'Video uploads arrive with the Video service in Phase 2.',
      infoOnly: true,
    });
  }

  signOut(): void {
    this.menu.set(null);
    this.auth.logout();
  }
}

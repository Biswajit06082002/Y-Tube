import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { Video, VideoService } from '../../core/videos/video.service';
import { countLabel, timeAgo } from '../../shared/format';
import { Profile, ProfileService } from '../../core/users/profile.service';
import { ChannelAvatar } from '../../shared/channel-avatar';
import { SignInEmptyState } from '../../shared/sign-in-empty-state';
import { VideoThumbnail } from '../../shared/video-thumbnail';
import { VideoCard } from '../../shared/video-card';

/**
 * Login-only pages. Logged-out visitors see a friendly "Sign in" empty state (like YouTube)
 * instead of being bounced to the login page; the API would answer 401 anyway.
 */

@Component({
  selector: 'app-subscriptions',
  imports: [SignInEmptyState, RouterLink, VideoCard, ChannelAvatar],
  template: `
    @if (auth.isLoggedIn()) {
      @if (creators().length) {
        <div class="pt-2">
          <h1 class="mb-4 px-2 font-display text-3xl font-bold tracking-tight">Following</h1>

          <!-- Creators you follow -->
          <div class="glass mb-6 flex gap-5 overflow-x-auto rounded-3xl p-4 [scrollbar-width:none]">
            @for (c of creators(); track c.userId) {
              <a [routerLink]="['/channel', c.userId]" class="group flex w-20 shrink-0 flex-col items-center gap-2 text-center">
                <span class="rounded-[38%] transition group-hover:ring-2 group-hover:ring-accent/60">
                  <app-channel-avatar [name]="c.displayName" [size]="56" />
                </span>
                <span class="w-full truncate text-xs font-medium">{{ c.displayName }}</span>
              </a>
            }
          </div>

          @if (videos().length) {
            <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
              @for (v of videos(); track v.id) {
                <app-video-card [video]="v" />
              }
            </div>
          } @else if (loaded()) {
            <p class="px-2 text-muted">The creators you follow haven't published anything yet.</p>
          }
        </div>
      } @else if (loaded()) {
        <div class="panel mx-auto mt-6 flex max-w-xl flex-col items-center px-6 py-16 text-center">
          <h2 class="font-display text-2xl font-semibold">Not following anyone yet</h2>
          <p class="mt-2 max-w-md text-muted">
            Follow creators from their channel or a video page, and their new videos will show up here.
          </p>
          <a routerLink="/" class="btn-primary mt-6">Discover videos</a>
        </div>
      }
    } @else {
      <app-sign-in-empty-state
        icon="subscriptions"
        title="Never miss a drop"
        message="Sign in to follow creators and get their new videos here."
      />
    }
  `,
})
export class Subscriptions implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly profiles = inject(ProfileService);
  private readonly videoService = inject(VideoService);

  protected readonly creators = signal<Profile[]>([]);
  protected readonly videos = signal<Video[]>([]);
  protected readonly loaded = signal(false);

  ngOnInit(): void {
    if (!this.auth.isLoggedIn()) return;
    this.profiles.following().subscribe({ next: (list) => this.creators.set(list), error: () => {} });
    this.videoService.subscriptions().subscribe({
      next: (page) => {
        this.videos.set(page.items);
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true),
    });
  }
}

@Component({
  selector: 'app-history',
  imports: [SignInEmptyState],
  template: `
    @if (auth.isLoggedIn()) {
      <div class="panel mx-auto mt-6 flex max-w-xl flex-col items-center px-6 py-16 text-center">
        <h2 class="font-display text-2xl font-semibold">Watch history</h2>
        <p class="mt-2 max-w-md text-muted">
          Watch history arrives once playback exists. It will also feed the recommendations (the ML part!).
        </p>
      </div>
    } @else {
      <app-sign-in-empty-state
        icon="history"
        title="Keep track of what you watch"
        message="Watch history isn't viewable when signed out."
      />
    }
  `,
})
export class History {
  protected readonly auth = inject(AuthService);
}

@Component({
  selector: 'app-liked',
  imports: [SignInEmptyState, VideoCard, RouterLink],
  template: `
    @if (auth.isLoggedIn()) {
      <div class="pt-2">
        <h1 class="mb-4 px-2 font-display text-3xl font-bold tracking-tight">Liked</h1>
        @if (videos().length) {
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
            @for (v of videos(); track v.id) {
              <app-video-card [video]="v" />
            }
          </div>
        } @else if (loaded()) {
          <p class="px-2 text-muted">
            Videos you like will show up here. <a routerLink="/" class="font-semibold text-accent hover:underline">Find something to like</a>
          </p>
        }
      </div>
    } @else {
      <app-sign-in-empty-state icon="like" title="Enjoy your favorite videos" message="Sign in to access videos that you've liked." />
    }
  `,
})
export class Liked implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly videoService = inject(VideoService);

  protected readonly videos = signal<Video[]>([]);
  protected readonly loaded = signal(false);

  ngOnInit(): void {
    if (!this.auth.isLoggedIn()) return;
    this.videoService.liked().subscribe({
      next: (list) => {
        this.videos.set(list);
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true),
    });
  }
}

@Component({
  selector: 'app-my-videos',
  imports: [SignInEmptyState, RouterLink, VideoThumbnail],
  template: `
    @if (auth.isLoggedIn()) {
      <div class="pt-2">
        <div class="mb-4 flex items-center justify-between px-2">
          <h1 class="font-display text-3xl font-bold tracking-tight">Your videos</h1>
          <a routerLink="/upload" class="btn-primary">Upload</a>
        </div>

        @if (videos().length) {
          <div class="panel divide-y divide-[var(--glass-border)] overflow-hidden">
            @for (v of videos(); track v.id) {
              <a [routerLink]="['/watch', v.id]" class="flex items-center gap-4 p-3 transition hover:bg-fg/5">
                <div class="w-36 shrink-0 sm:w-44">
                  <app-video-thumbnail [videoId]="v.id" [title]="v.title" [imageUrl]="v.thumbnailUrl" [seconds]="v.durationSeconds" rounded="rounded-xl" />
                </div>
                <div class="min-w-0 flex-1">
                  <p class="line-clamp-2 font-semibold">{{ v.title }}</p>
                  <p class="mt-0.5 text-xs text-muted">{{ v.category }} · uploaded {{ ago(v.publishedAtUtc) }}</p>
                  @if (v.status === 'Ready') {
                    <p class="mt-1 text-xs text-muted">{{ views(v.views, 'view') }} · {{ v.qualities.join(' · ') }}</p>
                  }
                </div>
                <span class="shrink-0 rounded-full px-3 py-1 text-xs font-semibold" [class]="badgeClass(v.status)">
                  {{ v.status === 'Processing' ? 'Processing ' + (v.processingProgress ?? 0) + '%' : v.status }}
                </span>
              </a>
            }
          </div>
        } @else if (loaded()) {
          <p class="px-2 text-muted">
            You haven't uploaded anything yet. <a routerLink="/upload" class="font-semibold text-accent hover:underline">Upload your first video</a>
          </p>
        }
      </div>
    } @else {
      <app-sign-in-empty-state icon="library" title="Share your videos" message="Sign in to upload videos and manage them here." />
    }
  `,
})
export class MyVideos implements OnInit, OnDestroy {
  protected readonly auth = inject(AuthService);
  private readonly videoService = inject(VideoService);

  protected readonly videos = signal<Video[]>([]);
  protected readonly loaded = signal(false);
  protected readonly ago = timeAgo;
  protected readonly views = countLabel;
  private timer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    if (this.auth.isLoggedIn()) this.refresh();
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }

  protected badgeClass(status: Video['status']): string {
    switch (status) {
      case 'Ready':
        return 'bg-emerald-500/15 text-emerald-500';
      case 'Failed':
        return 'bg-red-500/15 text-red-500';
      default:
        return 'bg-accent/15 text-accent';
    }
  }

  /** Refreshes every 4 s while something is still processing (SignalR replaces this next). */
  private refresh(): void {
    this.videoService.mine().subscribe({
      next: (list) => {
        this.videos.set(list);
        this.loaded.set(true);
        if (list.some((v) => v.status === 'Queued' || v.status === 'Processing')) {
          this.timer = setTimeout(() => this.refresh(), 4000);
        }
      },
      error: () => this.loaded.set(true),
    });
  }
}

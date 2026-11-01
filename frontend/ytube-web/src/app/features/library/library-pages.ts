import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { Video, VideoService } from '../../core/videos/video.service';
import { SignInEmptyState } from '../../shared/sign-in-empty-state';
import { VideoCard } from '../../shared/video-card';

/**
 * Login-only pages. Logged-out visitors see a friendly "Sign in" empty state (like YouTube)
 * instead of being bounced to the login page; the API would answer 401 anyway.
 */

@Component({
  selector: 'app-subscriptions',
  imports: [SignInEmptyState, RouterLink],
  template: `
    @if (auth.isLoggedIn()) {
      <div class="panel mx-auto mt-6 flex max-w-xl flex-col items-center px-6 py-16 text-center">
        <h2 class="font-display text-2xl font-semibold">Not following anyone yet</h2>
        <p class="mt-2 max-w-md text-muted">
          New videos from creators you follow will show up here. Following arrives with the User service.
        </p>
        <a routerLink="/" class="btn-primary mt-6">Discover videos</a>
      </div>
    } @else {
      <app-sign-in-empty-state
        icon="subscriptions"
        title="Never miss a drop"
        message="Sign in to follow creators and get their new videos here."
      />
    }
  `,
})
export class Subscriptions {
  protected readonly auth = inject(AuthService);
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

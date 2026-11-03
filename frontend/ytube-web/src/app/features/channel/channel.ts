import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../../core/api/api.models';
import { SignInPromptService } from '../../core/auth/sign-in-prompt.service';
import { Profile, ProfileService } from '../../core/users/profile.service';
import { ChannelStats, Video, VideoService } from '../../core/videos/video.service';
import { ChannelAvatar } from '../../shared/channel-avatar';
import { compactNumber } from '../../shared/format';
import { Icon } from '../../shared/icon';
import { VideoCard } from '../../shared/video-card';

const PAGE_SIZE = 12;

/**
 * A creator's channel: profile from the User service + videos and totals from the Video service.
 * The page composes three API calls itself ("client-side composition"); a bigger app might add a
 * backend-for-frontend that does this in one round trip.
 */
@Component({
  selector: 'app-channel',
  imports: [RouterLink, ChannelAvatar, Icon, VideoCard],
  template: `
    @if (error()) {
      <div class="panel mx-auto mt-10 max-w-md p-10 text-center">
        <p class="text-lg">{{ error() }}</p>
        <a routerLink="/" class="btn-soft mt-4">Back to Discover</a>
      </div>
    } @else if (profile(); as p) {
      <!-- Header -->
      <section class="panel relative mt-1 overflow-hidden">
        <div class="brand-gradient relative h-36 sm:h-44">
          <div class="absolute -top-10 -right-10 h-64 w-64 rounded-full bg-white/15 blur-2xl"></div>
          <div class="absolute -bottom-16 left-1/4 h-64 w-64 rounded-full bg-black/15 blur-2xl"></div>
        </div>

        <div class="flex flex-col gap-5 px-6 pb-6 sm:flex-row sm:items-end">
          <div class="-mt-14 w-fit rounded-[38%] ring-4 ring-[var(--bg)] sm:-mt-16">
            <app-channel-avatar [name]="p.displayName" [size]="112" />
          </div>

          <div class="min-w-0 flex-1">
            <h1 class="truncate font-display text-3xl font-bold tracking-tight">{{ p.displayName }}</h1>
            <p class="mt-1 text-sm text-muted">
              <span class="font-medium text-fg">&#64;{{ p.handle }}</span>
              · {{ count(p.followerCount) }} {{ p.followerCount === 1 ? 'follower' : 'followers' }}
              · {{ stats()?.videoCount ?? '–' }} {{ stats()?.videoCount === 1 ? 'video' : 'videos' }}
            </p>
            @if (p.bio) {
              <p class="mt-2 line-clamp-2 max-w-2xl text-sm text-muted">{{ p.bio }}</p>
            }
          </div>

          <div class="flex shrink-0 gap-2">
            @if (p.isMe) {
              <button (click)="openEditor()" class="btn-soft"><app-icon name="settings" [size]="18" /> Edit profile</button>
              <a routerLink="/upload" class="btn-primary"><app-icon name="upload" [size]="18" /> Upload</a>
            } @else {
              <button
                (click)="toggleFollow()"
                [disabled]="following()"
                [class]="p.isFollowedByMe ? 'btn-soft' : 'btn-primary'"
              >
                @if (p.isFollowedByMe) {
                  <app-icon name="check" [size]="18" /> Following
                } @else {
                  Follow
                }
              </button>
            }
          </div>
        </div>

        <!-- Tabs -->
        <div class="flex gap-1 border-t border-[var(--glass-border)] px-4" role="tablist">
          @for (t of tabs; track t) {
            <button
              (click)="tab.set(t)"
              role="tab"
              [attr.aria-selected]="tab() === t"
              class="relative px-4 py-3 text-sm font-semibold transition"
              [class]="tab() === t ? 'text-fg' : 'text-muted hover:text-fg'"
            >
              {{ t }}
              @if (tab() === t) {
                <span class="brand-gradient absolute inset-x-3 bottom-0 h-0.5 rounded-full"></span>
              }
            </button>
          }
        </div>
      </section>

      @if (tab() === 'Videos') {
        <div class="mt-5">
          @if (videos().length) {
            <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
              @for (v of videos(); track v.id) {
                <app-video-card [video]="v" [hideChannel]="true" />
              }
            </div>
            @if (hasMore()) {
              <div class="mt-6 text-center">
                <button (click)="loadMore()" [disabled]="loadingVideos()" class="btn-soft">
                  {{ loadingVideos() ? 'Loading…' : 'Show more' }}
                </button>
              </div>
            }
          } @else if (!loadingVideos()) {
            <div class="panel p-10 text-center text-muted">
              @if (p.isMe) {
                You haven't published any videos yet. <a routerLink="/upload" class="font-semibold text-accent hover:underline">Upload one</a>
              } @else {
                This channel hasn't published any videos yet.
              }
            </div>
          }
        </div>
      } @else {
        <!-- About -->
        <div class="mt-5 grid gap-5 lg:grid-cols-[2fr_1fr]">
          <section class="panel p-6">
            <h2 class="mb-3 font-display text-lg font-semibold">About</h2>
            <p class="text-sm leading-relaxed whitespace-pre-line text-muted">
              {{ p.bio || (p.isMe ? 'Tell viewers about your channel — click “Edit profile”.' : 'No description yet.') }}
            </p>
          </section>
          <section class="panel space-y-4 p-6">
            <h2 class="font-display text-lg font-semibold">Stats</h2>
            @for (row of statRows(); track row.label) {
              <div class="flex items-center justify-between text-sm">
                <span class="text-muted">{{ row.label }}</span>
                <span class="font-semibold">{{ row.value }}</span>
              </div>
            }
          </section>
        </div>
      }

      <!-- Edit profile -->
      @if (editing()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/30 p-4 backdrop-blur-md" (click)="editing.set(false)">
          <div class="glass-strong w-full max-w-lg space-y-4 rounded-3xl p-6" (click)="$event.stopPropagation()" role="dialog" aria-modal="true">
            <h2 class="font-display text-xl font-semibold">Edit profile</h2>

            <label class="block">
              <span class="mb-1.5 flex justify-between text-sm font-medium">Name <span class="text-xs font-normal text-muted">{{ draftName().length }}/50</span></span>
              <input class="input" maxlength="50" [value]="draftName()" (input)="draftName.set(value($event))" />
            </label>

            <label class="block">
              <span class="mb-1.5 block text-sm font-medium">Handle</span>
              <div class="input flex items-center gap-1 !py-0">
                <span class="text-muted">&#64;</span>
                <input class="w-full bg-transparent py-3 outline-none" maxlength="30" [value]="draftHandle()" (input)="draftHandle.set(value($event))" />
              </div>
              <span class="mt-1 block text-xs text-muted">3–30 characters: letters, numbers, “_” and “.”</span>
            </label>

            <label class="block">
              <span class="mb-1.5 flex justify-between text-sm font-medium">Description <span class="text-xs font-normal text-muted">{{ draftBio().length }}/1000</span></span>
              <textarea class="input min-h-28 resize-y" maxlength="1000" [value]="draftBio()" (input)="draftBio.set(value($event))"></textarea>
            </label>

            @if (editError()) {
              <p class="rounded-xl bg-red-500/10 px-3 py-2 text-sm text-red-500">{{ editError() }}</p>
            }

            <div class="flex justify-end gap-2 pt-2">
              <button (click)="editing.set(false)" class="btn-soft">Cancel</button>
              <button (click)="saveProfile()" [disabled]="saving()" class="btn-primary">{{ saving() ? 'Saving…' : 'Save' }}</button>
            </div>
          </div>
        </div>
      }
    } @else {
      <div class="panel mt-1 h-72 animate-pulse"></div>
    }
  `,
})
export class Channel {
  private readonly profiles = inject(ProfileService);
  private readonly videoService = inject(VideoService);
  private readonly gate = inject(SignInPromptService);

  /** :id route param (the creator's user id). */
  readonly id = input.required<string>();

  protected readonly tabs = ['Videos', 'About'] as const;
  protected readonly tab = signal<'Videos' | 'About'>('Videos');
  protected readonly count = compactNumber;

  protected readonly profile = signal<Profile | null>(null);
  protected readonly stats = signal<ChannelStats | null>(null);
  protected readonly videos = signal<Video[]>([]);
  protected readonly hasMore = signal(false);
  protected readonly loadingVideos = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly following = signal(false);
  private page = 0;

  protected readonly editing = signal(false);
  protected readonly saving = signal(false);
  protected readonly editError = signal<string | null>(null);
  protected readonly draftName = signal('');
  protected readonly draftHandle = signal('');
  protected readonly draftBio = signal('');

  protected readonly statRows = computed(() => {
    const p = this.profile();
    const s = this.stats();
    return [
      { label: 'Joined', value: p ? new Date(p.joinedAtUtc).toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' }) : '–' },
      { label: 'Followers', value: compactNumber(p?.followerCount ?? 0) },
      { label: 'Following', value: compactNumber(p?.followingCount ?? 0) },
      { label: 'Videos', value: compactNumber(s?.videoCount ?? 0) },
      { label: 'Total views', value: compactNumber(s?.totalViews ?? 0) },
      { label: 'Total likes', value: compactNumber(s?.totalLikes ?? 0) },
    ];
  });

  constructor() {
    // Reload when the channel changes *or* the viewer signs in/out: "Edit profile" vs "Follow"
    // and IsFollowedByMe depend on who is looking.
    effect(() => {
      const id = this.id();
      this.profiles.myId();
      untracked(() => this.load(id));
    });
  }

  protected value(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  toggleFollow(): void {
    const p = this.profile();
    if (!p) return;
    if (!this.gate.require({ title: `Follow ${p.displayName}?`, message: 'Sign in to follow creators and see their new videos in Following.' })) return;

    // Optimistic: flip right away, then take the server's numbers (or roll back).
    const previous = p;
    this.profile.set({ ...p, isFollowedByMe: !p.isFollowedByMe, followerCount: p.followerCount + (p.isFollowedByMe ? -1 : 1) });
    this.following.set(true);
    this.profiles.setFollow(p.userId, !p.isFollowedByMe).subscribe({
      next: (updated) => {
        this.profile.set(updated);
        this.following.set(false);
      },
      error: () => {
        this.profile.set(previous);
        this.following.set(false);
      },
    });
  }

  openEditor(): void {
    const p = this.profile()!;
    this.draftName.set(p.displayName);
    this.draftHandle.set(p.handle);
    this.draftBio.set(p.bio);
    this.editError.set(null);
    this.editing.set(true);
  }

  saveProfile(): void {
    this.saving.set(true);
    this.editError.set(null);
    this.profiles.updateMe({ displayName: this.draftName(), handle: this.draftHandle(), bio: this.draftBio() }).subscribe({
      next: (updated) => {
        this.profile.set(updated);
        this.saving.set(false);
        this.editing.set(false);
        // Video cards carry a copy of the name, updated by an event — refresh them a moment later.
        setTimeout(() => this.reloadVideos(updated.userId), 1500);
      },
      error: (err) => {
        this.editError.set(errorMessage(err));
        this.saving.set(false);
      },
    });
  }

  loadMore(): void {
    const id = this.profile()?.userId;
    if (!id || this.loadingVideos()) return;
    this.loadingVideos.set(true);
    this.videoService.channelVideos(id, this.page + 1, PAGE_SIZE).subscribe({
      next: (res) => {
        this.page = res.page;
        this.videos.update((list) => [...list, ...res.items]);
        this.hasMore.set(res.hasMore);
        this.loadingVideos.set(false);
      },
      error: () => this.loadingVideos.set(false),
    });
  }

  private load(id: string): void {
    this.profile.set(null);
    this.stats.set(null);
    this.error.set(null);
    this.tab.set('Videos');

    this.profiles.profile(id).subscribe({
      next: (p) => {
        this.profile.set(p);
        this.reloadVideos(id);
      },
      error: (err) => this.error.set(err?.status === 404 ? "This channel doesn't exist." : errorMessage(err)),
    });
    this.videoService.channelStats(id).subscribe({ next: (s) => this.stats.set(s), error: () => {} });
  }

  private reloadVideos(id: string): void {
    this.page = 0;
    this.videos.set([]);
    this.loadMore();
    void id;
  }
}

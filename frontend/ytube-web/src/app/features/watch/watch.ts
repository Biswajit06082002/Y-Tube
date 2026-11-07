import { Component, OnDestroy, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../../core/api/api.models';
import { SignInPromptService } from '../../core/auth/sign-in-prompt.service';
import { Profile, ProfileService } from '../../core/users/profile.service';
import { Video, VideoService, mediaUrl } from '../../core/videos/video.service';
import { ChannelAvatar } from '../../shared/channel-avatar';
import { compactNumber, countLabel, duration, timeAgo } from '../../shared/format';
import { Icon } from '../../shared/icon';
import { VideoPlayer } from '../../shared/video-player';
import { RichText } from '../../shared/rich-text';
import { VideoThumbnail } from '../../shared/video-thumbnail';
import { Comments } from './comments';

/** Watch page: public to view; like / subscribe go through the sign-in gate. */
@Component({
  selector: 'app-watch',
  imports: [RouterLink, Icon, ChannelAvatar, VideoThumbnail, VideoPlayer, RichText, Comments],
  template: `
    <div class="mx-auto flex max-w-[1600px] flex-col gap-6 pt-2 xl:flex-row">
      <section class="min-w-0 flex-1">
        @if (error()) {
          <div class="panel py-24 text-center">
            <p class="text-lg">{{ error() }}</p>
            <a routerLink="/" class="btn-soft mt-4">Back to Discover</a>
          </div>
        } @else if (video(); as v) {
          @if (v.streamUrl) {
            <app-video-player [src]="stream()!" [poster]="poster()" (viewQualified)="countView()" />
          } @else if (v.status === 'Queued' || v.status === 'Processing') {
            <!-- Still being transcoded: live progress (polled for now; SignalR pushes it in the next step) -->
            <div class="relative overflow-hidden rounded-3xl bg-black">
              <app-video-thumbnail [videoId]="v.id" [title]="v.title" [imageUrl]="v.thumbnailUrl" rounded="rounded-3xl" />
              <div class="absolute inset-0 flex flex-col items-center justify-center gap-4 bg-black/60 px-6 text-center text-white backdrop-blur-sm">
                <span class="h-10 w-10 animate-spin rounded-full border-4 border-white/20 border-t-white"></span>
                <div>
                  <p class="font-display text-xl font-semibold">{{ v.status === 'Queued' ? 'Waiting for a processing worker…' : 'Processing your video…' }}</p>
                  <p class="mt-1 text-sm text-white/75">Creating 1080p → 144p versions so it plays smoothly on any connection.</p>
                </div>
                <div class="w-full max-w-sm">
                  <div class="h-2 overflow-hidden rounded-full bg-white/20">
                    <div class="brand-gradient h-full rounded-full transition-all duration-700" [style.width.%]="v.processingProgress ?? 0"></div>
                  </div>
                  <p class="mt-2 text-sm font-semibold">{{ v.processingProgress ?? 0 }}%</p>
                </div>
              </div>
            </div>
          } @else if (v.status === 'Failed') {
            <div class="panel flex aspect-video flex-col items-center justify-center gap-3 px-6 text-center">
              <p class="font-display text-xl font-semibold">This video couldn't be processed</p>
              <p class="text-sm text-muted">{{ v.processingError || 'Something went wrong.' }}</p>
              <a routerLink="/upload" class="btn-primary mt-2">Upload again</a>
            </div>
          } @else {
            <!-- Seeded sample videos have no media files -->
            <div class="relative overflow-hidden rounded-3xl shadow-2xl shadow-accent/20">
              <app-video-thumbnail [videoId]="v.id" [title]="v.title" rounded="rounded-3xl" />
              <div class="absolute inset-0 flex flex-col items-center justify-center gap-3 bg-black/35 px-4 text-center text-white">
                <span class="flex h-20 w-20 items-center justify-center rounded-full bg-white/90 text-neutral-900 shadow-xl">
                  <app-icon name="play" [size]="40" />
                </span>
                <span class="text-sm text-white/85">Sample video — upload your own to see real playback</span>
              </div>
            </div>
          }

          <div class="mt-5 flex flex-wrap items-start justify-between gap-4">
            <div class="min-w-0">
              <span class="text-xs font-semibold tracking-wider text-accent uppercase">{{ v.category }}</span>
              <h1 class="mt-1 font-display text-2xl leading-tight font-bold tracking-tight">{{ v.title }}</h1>
            </div>
            <div class="flex items-center gap-2">
              <button
                (click)="toggleLike()"
                [disabled]="liking()"
                class="inline-flex items-center gap-2 rounded-xl px-4 py-2 text-sm font-semibold transition active:scale-95"
                [class]="v.likedByMe ? 'brand-gradient text-white shadow-lg shadow-accent/30' : 'glass hover:bg-surface-2'"
                [attr.aria-pressed]="v.likedByMe"
              >
                <app-icon name="like" [size]="18" />
                {{ likes() }}
              </button>
              <button (click)="share()" class="btn-soft">
                <app-icon name="share" [size]="18" />
                {{ copied() ? 'Copied!' : 'Share' }}
              </button>
            </div>
          </div>

          <div class="panel mt-5 flex flex-wrap items-center justify-between gap-4 p-4">
            @if (v.ownerId) {
              <a [routerLink]="['/channel', v.ownerId]" class="group/creator flex items-center gap-3">
                <app-channel-avatar [name]="v.channelName" [size]="44" />
                <div>
                  <p class="font-semibold transition group-hover/creator:text-accent">{{ v.channelName }}</p>
                  <p class="text-xs text-muted">
                    @if (creator(); as c) {
                      {{ format(c.followerCount) }} {{ c.followerCount === 1 ? 'follower' : 'followers' }}
                    } @else {
                      Creator
                    }
                  </p>
                </div>
              </a>
              @if (v.isOwner) {
                <a [routerLink]="['/channel', v.ownerId]" class="btn-soft">Your channel</a>
              } @else {
                <button (click)="toggleFollow()" [disabled]="followBusy()" [class]="creator()?.isFollowedByMe ? 'btn-soft' : 'btn-primary'">
                  @if (creator()?.isFollowedByMe) {
                    <app-icon name="check" [size]="18" /> Following
                  } @else {
                    Follow
                  }
                </button>
              }
            } @else {
              <!-- Seeded sample videos have no real creator account -->
              <div class="flex items-center gap-3">
                <app-channel-avatar [name]="v.channelName" [size]="44" />
                <div>
                  <p class="font-semibold">{{ v.channelName }}</p>
                  <p class="text-xs text-muted">Sample channel</p>
                </div>
              </div>
            }
          </div>

          <!-- Description: collapsed to a few lines, the whole box expands on click (YouTube style) -->
          <div
            class="panel mt-4 p-4 text-sm transition"
            [class.cursor-pointer]="!descriptionOpen()"
            [class.hover:bg-fg/5]="!descriptionOpen()"
            (click)="descriptionOpen.set(true)"
          >
            <p class="flex flex-wrap items-center gap-x-2 gap-y-1 font-semibold">
              <span>{{ descriptionOpen() ? fullViews() : label(v.views, 'view') }}</span>
              <span [title]="publishedOn()">{{ descriptionOpen() ? publishedOn() : ago(v.publishedAtUtc) }}</span>
              @for (tag of v.tags; track tag) {
                <span class="font-medium text-accent">#{{ tag }}</span>
              }
            </p>
            @if (v.description) {
              <app-rich-text class="mt-2 block leading-relaxed" [class.line-clamp-3]="!descriptionOpen()" [text]="v.description" />
            } @else {
              <p class="mt-2 text-muted italic">No description.</p>
            }
            @if (descriptionOpen()) {
              <button type="button" class="mt-3 font-semibold text-muted transition hover:text-fg" (click)="$event.stopPropagation(); descriptionOpen.set(false)">Show less</button>
            } @else if (descriptionIsLong()) {
              <button type="button" class="mt-1 font-semibold text-muted transition hover:text-fg">…more</button>
            }
          </div>

          <app-comments [videoId]="v.id" />
        } @else {
          <div class="aspect-video animate-pulse rounded-3xl bg-surface-2"></div>
          <div class="mt-5 h-7 w-2/3 animate-pulse rounded-lg bg-surface-2"></div>
        }
      </section>

      <!-- Up next -->
      <aside class="w-full shrink-0 xl:w-[380px]">
        <h2 class="mb-3 px-2 font-display text-lg font-semibold">Up next</h2>
        <div class="space-y-1">
          @for (n of upNext(); track n.id) {
            <a [routerLink]="['/watch', n.id]" class="group flex gap-3 rounded-2xl border border-transparent p-2 transition hover:border-[var(--glass-border)] hover:bg-fg/5">
              <div class="w-40 shrink-0">
                <app-video-thumbnail [videoId]="n.id" [title]="n.title" [imageUrl]="n.thumbnailUrl" [seconds]="n.durationSeconds" rounded="rounded-xl" />
              </div>
              <div class="min-w-0 py-0.5">
                <h3 class="line-clamp-2 text-sm leading-snug font-semibold transition group-hover:text-accent">{{ n.title }}</h3>
                <p class="mt-1 text-xs text-muted">{{ n.channelName }}</p>
                <p class="text-xs text-muted">{{ label(n.views, 'view') }} · {{ ago(n.publishedAtUtc) }}</p>
              </div>
            </a>
          }
        </div>
      </aside>
    </div>
  `,
})
export class Watch implements OnDestroy {
  private readonly videos = inject(VideoService);
  private readonly gate = inject(SignInPromptService);
  private readonly profiles = inject(ProfileService);

  /** :id route param. */
  readonly id = input.required<string>();

  protected readonly video = signal<Video | null>(null);
  protected readonly upNext = signal<Video[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly liking = signal(false);
  protected readonly copied = signal(false);
  protected readonly creator = signal<Profile | null>(null);
  protected readonly followBusy = signal(false);
  protected readonly descriptionOpen = signal(false);

  protected readonly stream = computed(() => mediaUrl(this.video()?.streamUrl));
  protected readonly poster = computed(() => mediaUrl(this.video()?.thumbnailUrl));
  private pollTimer: ReturnType<typeof setTimeout> | undefined;

  protected readonly likes = computed(() => compactNumber(this.video()?.likeCount ?? 0));
  protected readonly length = computed(() => duration(this.video()?.durationSeconds ?? 0));

  protected readonly fullViews = computed(() => {
    const views = this.video()?.views ?? 0;
    return `${views.toLocaleString('en')} ${views === 1 ? 'view' : 'views'}`;
  });
  protected readonly publishedOn = computed(() => {
    const v = this.video();
    return v ? new Date(v.publishedAtUtc).toLocaleDateString('en', { year: 'numeric', month: 'short', day: 'numeric' }) : '';
  });
  /** Only offer "…more" when there's something hidden. */
  protected readonly descriptionIsLong = computed(() => {
    const d = this.video()?.description ?? '';
    return d.length > 220 || d.split('\n').length > 3;
  });

  protected readonly format = compactNumber;
  protected readonly ago = timeAgo;
  protected readonly label = countLabel;

  constructor() {
    // Re-runs when navigating from one video to another (same component instance).
    effect(() => {
      const id = this.id();
      untracked(() => this.load(id));
    });

    // Signing in/out on this page changes "Following" and IsOwner — refresh the viewer-specific parts.
    let first = true;
    effect(() => {
      this.profiles.myId();
      if (first) return void (first = false);
      untracked(() => this.refreshViewerState());
    });
  }

  toggleLike(): void {
    const v = this.video();
    if (!v) return;
    if (!this.gate.require({ title: 'Like this video?', message: 'Sign in to make your opinion count and find it later in Liked.' })) return;

    // Optimistic update: flip the button immediately, roll back if the server says no.
    const previous = v;
    this.video.set({ ...v, likedByMe: !v.likedByMe, likeCount: v.likeCount + (v.likedByMe ? -1 : 1) });
    this.liking.set(true);

    this.videos.setLike(v.id, !v.likedByMe).subscribe({
      next: (updated) => {
        this.video.set(updated);
        this.liking.set(false);
      },
      error: () => {
        this.video.set(previous);
        this.liking.set(false);
      },
    });
  }

  toggleFollow(): void {
    const c = this.creator();
    if (!c) return;
    if (!this.gate.require({ title: `Follow ${c.displayName}?`, message: 'Sign in to follow creators and see their new videos in Following.' })) return;

    // Optimistic update, then take the server's follower count (or roll back).
    const previous = c;
    this.creator.set({ ...c, isFollowedByMe: !c.isFollowedByMe, followerCount: c.followerCount + (c.isFollowedByMe ? -1 : 1) });
    this.followBusy.set(true);
    this.profiles.setFollow(c.userId, !c.isFollowedByMe).subscribe({
      next: (updated) => {
        this.creator.set(updated);
        this.followBusy.set(false);
      },
      error: () => {
        this.creator.set(previous);
        this.followBusy.set(false);
      },
    });
  }

  private refreshViewerState(): void {
    const v = this.video();
    if (!v) return;
    this.videos.video(v.id).subscribe({ next: (fresh) => this.video.set(fresh), error: () => {} });
    if (v.ownerId) this.profiles.profile(v.ownerId).subscribe({ next: (p) => this.creator.set(p), error: () => {} });
  }

  /** Called by the player after a few seconds of real playback. */
  countView(): void {
    const v = this.video();
    if (!v) return;
    this.videos.recordView(v.id).subscribe({
      next: (res) => {
        // The database is updated in batches every few seconds; show the +1 right away.
        if (res.counted && this.video()?.id === v.id) this.video.update((cur) => (cur ? { ...cur, views: cur.views + 1 } : cur));
      },
      error: () => {},
    });
  }

  share(): void {
    navigator.clipboard?.writeText(window.location.href).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    });
  }

  ngOnDestroy(): void {
    clearTimeout(this.pollTimer);
  }

  private load(id: string): void {
    clearTimeout(this.pollTimer);
    this.video.set(null);
    this.creator.set(null);
    this.error.set(null);
    this.descriptionOpen.set(false);
    window.scrollTo({ top: 0 });

    this.videos.video(id).subscribe({
      next: (v) => {
        this.video.set(v);
        this.pollWhileProcessing(id);
        if (v.ownerId) {
          this.profiles.profile(v.ownerId).subscribe({ next: (p) => this.creator.set(p), error: () => {} });
        }
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
    this.videos.feed(1, 16, null).subscribe({
      next: (page) => this.upNext.set(page.items.filter((v) => v.id !== id)),
      error: () => {},
    });
  }

  /**
   * While the video is queued/processing, re-fetch it every 2.5 s to show progress.
   * Temporary: the next step replaces this polling with SignalR pushing updates in real time.
   */
  private pollWhileProcessing(id: string): void {
    const status = this.video()?.status;
    if (status !== 'Queued' && status !== 'Processing') return;

    this.pollTimer = setTimeout(() => {
      this.videos.video(id).subscribe({
        next: (v) => {
          if (this.id() !== id) return;
          this.video.set(v);
          this.pollWhileProcessing(id);
        },
        error: () => this.pollWhileProcessing(id),
      });
    }, 2500);
  }
}

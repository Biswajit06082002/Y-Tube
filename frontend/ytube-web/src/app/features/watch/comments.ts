import { Component, DestroyRef, ElementRef, afterNextRender, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { errorMessage } from '../../core/api/api.models';
import { SignInPromptService } from '../../core/auth/sign-in-prompt.service';
import { Comment, CommentService } from '../../core/comments/comment.service';
import { ProfileService } from '../../core/users/profile.service';
import { compactNumber } from '../../shared/format';
import { Icon } from '../../shared/icon';
import { CommentComposer, CommentSubmit } from './comment-composer';
import { CommentThread } from './comment-thread';

/**
 * The comments section under a video. Anyone can read; writing goes through the sign-in gate.
 * Loads 20 threads at a time with a cursor, fetching the next page as you scroll near the end.
 */
@Component({
  selector: 'app-comments',
  imports: [Icon, CommentComposer, CommentThread],
  template: `
    <section class="mt-8" aria-labelledby="comments-heading">
      <h2 id="comments-heading" class="flex items-center gap-2 font-display text-xl font-semibold">
        <app-icon name="comment" [size]="22" class="text-accent" />
        @if (total() === null) {
          Comments
        } @else {
          {{ format(total()!) }} {{ total() === 1 ? 'Comment' : 'Comments' }}
        }
      </h2>

      <div class="mt-5">
        @if (profiles.myId()) {
          <app-comment-composer (submitted)="post($event)" />
          @if (postError()) {
            <p class="mt-2 ml-[52px] text-sm text-red-400">{{ postError() }}</p>
          }
        } @else {
          <!-- Guests see the box, but focusing it asks them to sign in first -->
          <button type="button" class="flex w-full items-center gap-3 text-left" (click)="askToSignIn()">
            <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-[38%] bg-fg/8 text-muted">
              <app-icon name="account" [size]="22" />
            </span>
            <span class="flex-1 border-b border-line pb-1.5 text-sm text-muted">Add a comment…</span>
          </button>
        }
      </div>

      @if (error()) {
        <div class="panel mt-6 p-5 text-sm">
          <p>{{ error() }}</p>
          <button type="button" class="btn-soft mt-3" (click)="reload()">Try again</button>
        </div>
      } @else {
        <div class="mt-7 space-y-6">
          @for (c of comments(); track c.id) {
            <app-comment-thread [comment]="c" (changed)="replace($event)" (removed)="remove($event)" />
          } @empty {
            @if (!loading()) {
              <div class="py-8 text-center text-sm text-muted">
                <p class="font-medium text-fg">No comments yet</p>
                <p class="mt-1">Be the first to share what you think.</p>
              </div>
            }
          }
        </div>

        @if (loading()) {
          <div class="mt-6 space-y-6" aria-label="Loading comments">
            @for (i of [1, 2, 3]; track i) {
              <div class="flex gap-3">
                <div class="h-10 w-10 shrink-0 animate-pulse rounded-[38%] bg-surface-2"></div>
                <div class="flex-1 space-y-2 pt-1">
                  <div class="h-3 w-32 animate-pulse rounded bg-surface-2"></div>
                  <div class="h-3 w-3/4 animate-pulse rounded bg-surface-2"></div>
                </div>
              </div>
            }
          </div>
        }
        <!-- Reaching this marker loads the next page (infinite scroll) -->
        <div #sentinel class="h-px"></div>
        @if (!loading() && nextCursor() !== null) {
          <div class="mt-4 text-center">
            <button type="button" class="btn-soft" (click)="loadMore()">Show more comments</button>
          </div>
        }
      }
    </section>
  `,
})
export class Comments {
  private readonly api = inject(CommentService);
  private readonly gate = inject(SignInPromptService);
  protected readonly profiles = inject(ProfileService);

  readonly videoId = input.required<string>();

  protected readonly comments = signal<Comment[]>([]);
  protected readonly total = signal<number | null>(null);
  protected readonly nextCursor = signal<number | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly postError = signal<string | null>(null);

  protected readonly format = compactNumber;
  private readonly destroyRef = inject(DestroyRef);
  private readonly sentinel = viewChild<ElementRef<HTMLElement>>('sentinel');
  private request = 0;

  constructor() {
    // New video, or someone signed in/out (Edit/Delete rights change) → start over.
    effect(() => {
      this.videoId();
      this.profiles.myId();
      untracked(() => this.reload());
    });

    afterNextRender(() => {
      const observer = new IntersectionObserver(
        (entries) => {
          if (entries.some((e) => e.isIntersecting) && !this.loading() && this.nextCursor() !== null) this.loadMore();
        },
        { rootMargin: '400px' },
      );
      const el = this.sentinel()?.nativeElement;
      if (el) observer.observe(el);
      this.destroyRef.onDestroy(() => observer.disconnect());
    });
  }

  protected reload(): void {
    this.comments.set([]);
    this.total.set(null);
    this.nextCursor.set(null);
    this.error.set(null);
    this.fetch(null);
  }

  protected loadMore(): void {
    this.fetch(this.nextCursor());
  }

  private fetch(cursor: number | null): void {
    const id = ++this.request; // ignore answers for a video we already left
    this.loading.set(true);
    this.api.forVideo(this.videoId(), cursor).subscribe({
      next: (page) => {
        if (id !== this.request) return;
        const known = new Set(this.comments().map((c) => c.id));
        this.comments.update((list) => [...list, ...page.items.filter((c) => !known.has(c.id))]);
        if (page.totalCount !== null) this.total.set(page.totalCount);
        this.nextCursor.set(page.nextCursor);
        this.loading.set(false);
      },
      error: (err) => {
        if (id !== this.request) return;
        this.error.set(errorMessage(err));
        this.loading.set(false);
      },
    });
  }

  protected askToSignIn(): void {
    this.gate.require({ title: 'Join the conversation', message: 'Sign in to comment, reply and tag people with @.' });
  }

  protected post(submit: CommentSubmit): void {
    this.postError.set(null);
    this.api.post(this.videoId(), submit.text).subscribe({
      next: (comment) => {
        submit.done(true);
        this.comments.update((list) => [comment, ...list]); // newest first, so it goes on top
        this.total.update((n) => (n ?? 0) + 1);
      },
      error: (err) => {
        submit.done(false);
        this.postError.set(errorMessage(err));
      },
    });
  }

  protected replace(updated: Comment): void {
    const before = this.comments().find((c) => c.id === updated.id);
    if (before) this.total.update((n) => (n ?? 0) + (updated.replyCount - before.replyCount));
    this.comments.update((list) => list.map((c) => (c.id === updated.id ? updated : c)));
  }

  protected remove(removed: Comment): void {
    this.comments.update((list) => list.filter((c) => c.id !== removed.id));
    this.total.update((n) => Math.max(0, (n ?? 0) - 1 - removed.replyCount));
  }
}

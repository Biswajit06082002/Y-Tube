import { Component, inject, input, output, signal } from '@angular/core';
import { errorMessage } from '../../core/api/api.models';
import { SignInPromptService } from '../../core/auth/sign-in-prompt.service';
import { Comment, CommentService } from '../../core/comments/comment.service';
import { ProfileService } from '../../core/users/profile.service';
import { countLabel } from '../../shared/format';
import { Icon } from '../../shared/icon';
import { CommentComposer, CommentSubmit } from './comment-composer';
import { CommentItem } from './comment-item';

/**
 * A top-level comment and its replies. Replies are one level deep (like YouTube): answering a
 * reply adds to the same thread, prefilled with "@handle " so it's clear who you're answering.
 */
@Component({
  selector: 'app-comment-thread',
  imports: [Icon, CommentItem, CommentComposer],
  template: `
    @let c = comment();
    <app-comment-item [comment]="c" (reply)="openReply($event)" (changed)="changed.emit($event)" (removed)="removed.emit($event)" />

    <div class="ml-[52px]">
      @if (replyingTo(); as target) {
        <div class="mt-3">
          <app-comment-composer
            [compact]="true"
            [autofocus]="true"
            [initialText]="prefill(target)"
            submitLabel="Reply"
            placeholder="Add a reply…"
            (submitted)="postReply($event)"
            (cancelled)="replyingTo.set(null)"
          />
          @if (replyError()) {
            <p class="mt-1 text-xs text-red-400">{{ replyError() }}</p>
          }
        </div>
      }

      @if (c.replyCount > 0) {
        <button
          type="button"
          class="mt-1 inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-sm font-semibold text-accent transition hover:bg-accent/10"
          [attr.aria-expanded]="open()"
          (click)="toggleReplies()"
        >
          <app-icon name="expand" [size]="20" [class.rotate-180]="open()" class="transition-transform" />
          {{ open() ? 'Hide replies' : label(c.replyCount, 'reply', 'replies') }}
        </button>
      }

      @if (open()) {
        <div class="mt-2 space-y-4">
          @for (r of replies(); track r.id) {
            <app-comment-item [comment]="r" (reply)="openReply($event)" (changed)="replaceReply($event)" (removed)="removeReply($event)" />
          }
        </div>
        @if (loading()) {
          <p class="mt-3 text-xs text-muted">Loading replies…</p>
        } @else if (nextCursor() !== null) {
          <button type="button" class="mt-2 inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-sm font-semibold text-accent transition hover:bg-accent/10" (click)="loadReplies()">
            <app-icon name="expand" [size]="20" /> Show more replies
          </button>
        }
      }
    </div>
  `,
})
export class CommentThread {
  private readonly comments = inject(CommentService);
  private readonly gate = inject(SignInPromptService);
  private readonly profiles = inject(ProfileService);

  readonly comment = input.required<Comment>();
  readonly changed = output<Comment>();
  readonly removed = output<Comment>();

  protected readonly open = signal(false);
  protected readonly replies = signal<Comment[]>([]);
  protected readonly nextCursor = signal<number | null>(null);
  protected readonly loading = signal(false);
  protected readonly replyingTo = signal<Comment | null>(null);
  protected readonly replyError = signal<string | null>(null);
  private loaded = false;

  protected readonly label = countLabel;

  protected openReply(target: Comment): void {
    if (!this.gate.require({ title: 'Join the conversation', message: 'Sign in to reply to comments.' })) return;
    this.replyError.set(null);
    this.replyingTo.set(target);
  }

  /** Answering someone else? Start with their @handle (YouTube style). */
  protected prefill(target: Comment): string {
    return target.authorId === this.profiles.myId() ? '' : `@${target.authorHandle} `;
  }

  protected toggleReplies(): void {
    this.open.set(!this.open());
    if (this.open() && !this.loaded) this.loadReplies();
  }

  protected loadReplies(): void {
    this.loading.set(true);
    this.comments.replies(this.comment().id, this.loaded ? this.nextCursor() : null).subscribe({
      next: (page) => {
        // Skip replies we already show (e.g. one we just posted before expanding).
        const known = new Set(this.replies().map((r) => r.id));
        this.replies.update((list) => [...list, ...page.items.filter((r) => !known.has(r.id))]);
        this.nextCursor.set(page.nextCursor);
        this.loaded = true;
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected postReply(submit: CommentSubmit): void {
    const parent = this.comment();
    this.comments.post(parent.videoId, submit.text, parent.id).subscribe({
      next: (reply) => {
        submit.done(true);
        this.replyingTo.set(null);
        this.replies.update((list) => [...list, reply]);
        this.open.set(true);
        this.changed.emit({ ...parent, replyCount: parent.replyCount + 1 });
      },
      error: (err) => {
        submit.done(false);
        this.replyError.set(errorMessage(err));
      },
    });
  }

  protected replaceReply(updated: Comment): void {
    this.replies.update((list) => list.map((r) => (r.id === updated.id ? updated : r)));
  }

  protected removeReply(removed: Comment): void {
    this.replies.update((list) => list.filter((r) => r.id !== removed.id));
    const parent = this.comment();
    this.changed.emit({ ...parent, replyCount: Math.max(0, parent.replyCount - 1) });
  }
}

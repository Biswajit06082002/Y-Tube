import { Component, ElementRef, computed, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../../core/api/api.models';
import { Comment, CommentService } from '../../core/comments/comment.service';
import { ChannelAvatar } from '../../shared/channel-avatar';
import { timeAgo } from '../../shared/format';
import { Icon } from '../../shared/icon';
import { RichText } from '../../shared/rich-text';
import { CommentComposer, CommentSubmit } from './comment-composer';

/** One comment or reply: author, text with @mentions, Reply, and Edit/Delete for those allowed. */
@Component({
  selector: 'app-comment-item',
  imports: [RouterLink, ChannelAvatar, Icon, RichText, CommentComposer],
  template: `
    @let c = comment();
    <div class="group/comment flex gap-3">
      <a [routerLink]="['/channel', c.authorId]" class="h-fit shrink-0 rounded-[38%] transition hover:ring-2 hover:ring-accent/50" [attr.aria-label]="c.authorName">
        <app-channel-avatar [name]="c.authorName" [size]="isReply() ? 28 : 40" />
      </a>

      <div class="min-w-0 flex-1">
        <div class="flex flex-wrap items-center gap-x-2 gap-y-0.5 text-xs">
          @if (c.isByCreator) {
            <a [routerLink]="['/channel', c.authorId]" class="rounded-full bg-fg/10 px-2 py-0.5 font-semibold text-fg transition hover:bg-fg/15">&#64;{{ c.authorHandle }}</a>
          } @else {
            <a [routerLink]="['/channel', c.authorId]" class="font-semibold transition hover:text-accent">&#64;{{ c.authorHandle }}</a>
          }
          @if (c.isByCreator) {
            <span class="rounded-full bg-accent/15 px-2 py-0.5 text-[11px] font-semibold text-accent">Creator</span>
          }
          <span class="text-muted">{{ ago(c.createdAtUtc) }}{{ c.editedAtUtc ? ' (edited)' : '' }}</span>
        </div>

        @if (editing()) {
          <div class="mt-2">
            <app-comment-composer
              [initialText]="c.text"
              [autofocus]="true"
              [showAvatar]="false"
              submitLabel="Save"
              placeholder="Edit your comment…"
              (submitted)="saveEdit($event)"
              (cancelled)="editing.set(false)"
            />
          </div>
        } @else {
          <app-rich-text
            class="mt-1 block text-sm leading-relaxed"
            [class.line-clamp-5]="!expanded()"
            [text]="c.text"
            [mentions]="c.mentions"
          />
          @if (isLong()) {
            <button type="button" class="mt-1 text-xs font-semibold text-muted transition hover:text-fg" (click)="expanded.set(!expanded())">
              {{ expanded() ? 'Show less' : 'Read more' }}
            </button>
          }

          <div class="mt-1.5 flex items-center gap-1">
            <button type="button" class="inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold text-muted transition hover:bg-fg/8 hover:text-fg" (click)="reply.emit(c)">
              <app-icon name="reply" [size]="15" /> Reply
            </button>

            @if (c.canEdit || c.canDelete) {
              <div class="relative">
                <button
                  type="button"
                  class="inline-flex h-8 w-8 items-center justify-center rounded-full text-muted transition hover:bg-fg/8 hover:text-fg sm:opacity-0 sm:group-hover/comment:opacity-100 sm:focus:opacity-100"
                  [class.sm:opacity-100]="menuOpen()"
                  aria-label="Comment actions"
                  [attr.aria-expanded]="menuOpen()"
                  (click)="menuOpen.set(!menuOpen())"
                >
                  <app-icon name="more" [size]="18" />
                </button>
                @if (menuOpen()) {
                  <div class="glass-strong absolute top-full left-0 z-20 mt-1 w-36 rounded-2xl p-1.5 text-sm">
                    @if (c.canEdit) {
                      <button type="button" class="flex w-full items-center gap-2.5 rounded-xl px-3 py-2 transition hover:bg-fg/8" (click)="startEdit()">
                        <app-icon name="edit" [size]="17" /> Edit
                      </button>
                    }
                    @if (c.canDelete) {
                      <button type="button" class="flex w-full items-center gap-2.5 rounded-xl px-3 py-2 text-red-400 transition hover:bg-red-500/10" (click)="askDelete()">
                        <app-icon name="delete" [size]="17" /> Delete
                      </button>
                    }
                  </div>
                }
              </div>
            }
          </div>

          @if (confirmingDelete()) {
            <div class="mt-2 flex flex-wrap items-center gap-2 rounded-2xl border border-red-500/30 bg-red-500/8 px-3 py-2 text-sm">
              <span class="mr-auto">
                Delete this {{ isReply() ? 'reply' : 'comment' }}{{ !isReply() && c.replyCount ? ' and its replies' : '' }}?
              </span>
              <button type="button" class="btn-soft !py-1.5" (click)="confirmingDelete.set(false)">Cancel</button>
              <button type="button" class="inline-flex items-center rounded-xl bg-red-500 px-4 py-1.5 text-sm font-semibold text-white transition hover:brightness-110 disabled:opacity-50" [disabled]="deleting()" (click)="confirmDelete()">
                {{ deleting() ? 'Deleting…' : 'Delete' }}
              </button>
            </div>
          }
          @if (error()) {
            <p class="mt-1 text-xs text-red-400">{{ error() }}</p>
          }
        }
      </div>
    </div>
  `,
  host: { '(document:click)': 'closeMenu($event)' },
})
export class CommentItem {
  private readonly comments = inject(CommentService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly comment = input.required<Comment>();

  readonly reply = output<Comment>();
  readonly changed = output<Comment>();
  readonly removed = output<Comment>();

  protected readonly editing = signal(false);
  protected readonly expanded = signal(false);
  protected readonly menuOpen = signal(false);
  protected readonly confirmingDelete = signal(false);
  protected readonly deleting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly isReply = computed(() => this.comment().parentId !== null);
  protected readonly isLong = computed(() => {
    const text = this.comment().text;
    return text.length > 320 || text.split('\n').length > 5;
  });

  protected readonly ago = timeAgo;

  protected closeMenu(event: MouseEvent): void {
    if (this.menuOpen() && !this.host.nativeElement.contains(event.target as Node)) this.menuOpen.set(false);
  }

  protected startEdit(): void {
    this.menuOpen.set(false);
    this.error.set(null);
    this.editing.set(true);
  }

  protected saveEdit(submit: CommentSubmit): void {
    this.comments.edit(this.comment().id, submit.text).subscribe({
      next: (updated) => {
        submit.done(true);
        this.editing.set(false);
        // Keep the reply count we have (the edit response is about the text).
        this.changed.emit({ ...updated, replyCount: this.comment().replyCount });
      },
      error: (err) => {
        submit.done(false);
        this.error.set(errorMessage(err));
      },
    });
  }

  protected askDelete(): void {
    this.menuOpen.set(false);
    this.error.set(null);
    this.confirmingDelete.set(true);
  }

  protected confirmDelete(): void {
    this.deleting.set(true);
    this.comments.delete(this.comment().id).subscribe({
      next: () => this.removed.emit(this.comment()),
      error: (err) => {
        this.deleting.set(false);
        this.error.set(errorMessage(err));
      },
    });
  }
}

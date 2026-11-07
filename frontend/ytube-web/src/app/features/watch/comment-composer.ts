import { Component, ElementRef, OnDestroy, afterNextRender, computed, inject, input, output, signal, viewChild } from '@angular/core';
import { MAX_COMMENT_LENGTH } from '../../core/comments/comment.service';
import { ProfileService, ProfileSummary } from '../../core/users/profile.service';
import { ChannelAvatar } from '../../shared/channel-avatar';

/** What the composer hands to its parent. Call done(true) when saved to clear the box. */
export interface CommentSubmit {
  text: string;
  done: (saved: boolean) => void;
}

// The "@partial" right before the caret, if the user is typing a mention.
const MENTION_AT_CARET = /(?:^|[^A-Za-z0-9_.@])@([A-Za-z0-9_.]{0,30})$/;

/**
 * Text box for comments, replies and edits, with @mention autocomplete:
 * type "@" + a letter → matching users appear → ↑/↓ + Enter (or click) inserts "@handle ".
 */
@Component({
  selector: 'app-comment-composer',
  imports: [ChannelAvatar],
  template: `
    <div class="flex gap-3">
      @if (showAvatar()) {
        <app-channel-avatar [name]="profiles.me()?.displayName ?? '?'" [size]="compact() ? 28 : 40" />
      }
      <div class="relative min-w-0 flex-1">
        <textarea
          #box
          rows="1"
          class="block w-full resize-none overflow-hidden border-b border-line bg-transparent pb-1.5 text-sm leading-relaxed outline-none transition placeholder:text-muted focus:border-accent"
          [placeholder]="placeholder()"
          [value]="text()"
          [attr.maxlength]="max"
          [attr.aria-label]="placeholder()"
          (focus)="active.set(true)"
          (input)="onInput()"
          (keydown)="onKeydown($event)"
          (click)="detectMention()"
          (blur)="closeSoon()"
        ></textarea>

        @if (suggestions().length) {
          <ul class="glass-strong absolute top-full left-0 z-30 mt-2 w-72 max-w-full overflow-hidden rounded-2xl p-1.5" role="listbox">
            @for (s of suggestions(); track s.userId; let i = $index) {
              <li
                role="option"
                [attr.aria-selected]="i === highlighted()"
                class="flex cursor-pointer items-center gap-2.5 rounded-xl px-2.5 py-2 text-sm transition"
                [class.bg-accent/15]="i === highlighted()"
                (mousedown)="$event.preventDefault(); pick(s)"
                (mouseenter)="highlighted.set(i)"
              >
                <app-channel-avatar [name]="s.displayName" [size]="28" />
                <span class="min-w-0">
                  <span class="block truncate font-medium">{{ s.displayName }}</span>
                  <span class="block truncate text-xs text-muted">&#64;{{ s.handle }}</span>
                </span>
              </li>
            }
          </ul>
        }

        @if (active() || text()) {
          <div class="mt-2 flex items-center justify-end gap-2">
            @if (text().length > max - 200) {
              <span class="mr-auto text-xs" [class.text-red-400]="text().length >= max" [class.text-muted]="text().length < max">
                {{ text().length }}/{{ max }}
              </span>
            }
            <button type="button" class="btn-soft !py-1.5" (click)="cancel()">Cancel</button>
            <button type="button" class="btn-primary !py-1.5" [disabled]="!canSubmit()" (click)="submit()">
              {{ busy() ? 'Saving…' : submitLabel() }}
            </button>
          </div>
        }
      </div>
    </div>
  `,
})
export class CommentComposer implements OnDestroy {
  protected readonly profiles = inject(ProfileService);

  readonly placeholder = input('Add a comment…');
  readonly submitLabel = input('Comment');
  readonly initialText = input('');
  readonly autofocus = input(false);
  readonly compact = input(false);
  readonly showAvatar = input(true);

  readonly submitted = output<CommentSubmit>();
  readonly cancelled = output<void>();

  protected readonly max = MAX_COMMENT_LENGTH;
  protected readonly text = signal('');
  protected readonly active = signal(false);
  protected readonly busy = signal(false);
  protected readonly suggestions = signal<ProfileSummary[]>([]);
  protected readonly highlighted = signal(0);
  protected readonly canSubmit = computed(() => !this.busy() && this.text().trim().length > 0 && this.text().length <= this.max);

  private readonly box = viewChild.required<ElementRef<HTMLTextAreaElement>>('box');
  private searchTimer: ReturnType<typeof setTimeout> | undefined;
  private closeTimer: ReturnType<typeof setTimeout> | undefined;
  private lastQuery = '';

  constructor() {
    afterNextRender(() => {
      this.text.set(this.initialText());
      if (this.autofocus()) {
        const el = this.box().nativeElement;
        el.focus();
        el.setSelectionRange(el.value.length, el.value.length); // caret after a prefilled "@handle "
      }
      this.autoGrow();
    });
  }

  protected onInput(): void {
    this.text.set(this.box().nativeElement.value);
    this.autoGrow();
    this.detectMention();
  }

  protected onKeydown(event: KeyboardEvent): void {
    const list = this.suggestions();
    if (list.length) {
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault();
        const step = event.key === 'ArrowDown' ? 1 : -1;
        this.highlighted.set((this.highlighted() + step + list.length) % list.length);
        return;
      }
      if (event.key === 'Enter' || event.key === 'Tab') {
        event.preventDefault();
        this.pick(list[this.highlighted()]);
        return;
      }
      if (event.key === 'Escape') {
        event.preventDefault();
        this.suggestions.set([]);
        return;
      }
    }
    // Ctrl/Cmd + Enter posts; a plain Enter is a new line (comments can have paragraphs).
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
      event.preventDefault();
      this.submit();
    }
  }

  /** Looks at the word before the caret; "@ab" → search users starting with "ab" (debounced). */
  protected detectMention(): void {
    const el = this.box().nativeElement;
    const before = el.value.slice(0, el.selectionStart ?? el.value.length);
    const query = MENTION_AT_CARET.exec(before)?.[1];

    clearTimeout(this.searchTimer);
    if (query === undefined || query.length === 0) {
      this.lastQuery = '';
      this.suggestions.set([]);
      return;
    }
    if (query === this.lastQuery) return;
    this.lastQuery = query;

    // Wait until typing pauses so we don't send a request per keystroke.
    this.searchTimer = setTimeout(() => {
      this.profiles.search(query).subscribe({
        next: (users) => {
          if (this.lastQuery !== query) return; // a newer search is on its way
          this.suggestions.set(users);
          this.highlighted.set(0);
        },
        error: () => this.suggestions.set([]),
      });
    }, 200);
  }

  /** Replaces the "@partial" before the caret with "@handle ". */
  protected pick(user: ProfileSummary): void {
    const el = this.box().nativeElement;
    const caret = el.selectionStart ?? el.value.length;
    const before = el.value.slice(0, caret).replace(/@[A-Za-z0-9_.]*$/, `@${user.handle} `);
    const value = before + el.value.slice(caret);

    el.value = value;
    this.text.set(value);
    el.focus();
    el.setSelectionRange(before.length, before.length);
    this.suggestions.set([]);
    this.lastQuery = '';
    this.autoGrow();
  }

  protected closeSoon(): void {
    // Delay so a click on a suggestion still lands.
    this.closeTimer = setTimeout(() => this.suggestions.set([]), 150);
  }

  protected cancel(): void {
    this.reset();
    this.cancelled.emit();
  }

  protected submit(): void {
    if (!this.canSubmit()) return;
    this.busy.set(true);
    this.submitted.emit({
      text: this.text().trim(),
      done: (saved) => {
        this.busy.set(false);
        if (saved) this.reset();
      },
    });
  }

  private reset(): void {
    this.text.set('');
    this.active.set(false);
    this.suggestions.set([]);
    const el = this.box().nativeElement;
    el.value = '';
    el.blur();
    this.autoGrow();
  }

  /** The textarea grows with its content instead of scrolling. */
  private autoGrow(): void {
    const el = this.box().nativeElement;
    el.style.height = 'auto';
    // Empty → back to one row (measuring now could catch a layout that isn't finished yet).
    el.style.height = el.value ? `${el.scrollHeight}px` : '';
  }

  ngOnDestroy(): void {
    clearTimeout(this.searchTimer);
    clearTimeout(this.closeTimer);
  }
}

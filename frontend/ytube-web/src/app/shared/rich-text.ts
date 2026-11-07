import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';

export interface MentionLink {
  userId: string;
  handle: string;
}

type Segment =
  | { kind: 'text'; value: string }
  | { kind: 'url'; value: string }
  | { kind: 'mention'; value: string; userId: string | null };

// A web link, or "@handle" that isn't part of a word or e-mail address (same rule as the server).
const TOKENS = /(https?:\/\/[^\s<]+[^\s<.,;:!?)\]'"])|(?<![A-Za-z0-9_.@])@([A-Za-z0-9_.]{3,30})/g;

/**
 * Plain user text with clickable links and @mentions. Mentions the server resolved to a user link to
 * their channel; unknown @handles are only highlighted. Built from text segments — never innerHTML —
 * so whatever people type can't inject markup.
 */
@Component({
  selector: 'app-rich-text',
  imports: [RouterLink],
  template: `
    @for (s of segments(); track $index) {
      @switch (s.kind) {
        @case ('url') {
          <a [href]="s.value" target="_blank" rel="noopener noreferrer nofollow" class="break-all text-accent hover:underline">{{ s.value }}</a>
        }
        @case ('mention') {
          @if (s.userId) {
            <a [routerLink]="['/channel', s.userId]" class="rounded-md bg-accent/12 px-1 font-medium text-accent transition hover:bg-accent/20">{{ s.value }}</a>
          } @else {
            <span class="font-medium text-accent/80">{{ s.value }}</span>
          }
        }
        @default {
          {{ s.value }}
        }
      }
    }
  `,
  host: { class: 'whitespace-pre-line break-words' },
})
export class RichText {
  readonly text = input.required<string>();
  readonly mentions = input<MentionLink[]>([]);

  protected readonly segments = computed<Segment[]>(() => {
    const text = this.text();
    const byHandle = new Map(this.mentions().map((m) => [m.handle.toLowerCase(), m.userId]));
    const out: Segment[] = [];
    let last = 0;

    for (const match of text.matchAll(TOKENS)) {
      const start = match.index ?? 0;
      if (start > last) out.push({ kind: 'text', value: text.slice(last, start) });

      if (match[1]) {
        out.push({ kind: 'url', value: match[1] });
        last = start + match[0].length;
      } else {
        // "@sam." at the end of a sentence: the dot isn't part of the handle.
        const handle = match[2].replace(/\.+$/, '');
        out.push({ kind: 'mention', value: `@${handle}`, userId: byHandle.get(handle.toLowerCase()) ?? null });
        last = start + 1 + handle.length;
      }
    }
    if (last < text.length) out.push({ kind: 'text', value: text.slice(last) });
    return out;
  });
}

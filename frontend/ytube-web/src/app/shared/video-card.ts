import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Video } from '../core/videos/video.service';
import { ChannelAvatar } from './channel-avatar';
import { countLabel, timeAgo } from './format';
import { VideoThumbnail } from './video-thumbnail';

/**
 * A feed card. The thumbnail/title open the video and the creator opens their channel — two
 * separate links side by side, because a link inside another link isn't valid HTML.
 */
@Component({
  selector: 'app-video-card',
  imports: [RouterLink, VideoThumbnail, ChannelAvatar],
  template: `
    @let v = video();
    <div class="glass glass-interactive group rounded-3xl p-2">
      <a [routerLink]="['/watch', v.id]" class="block">
        <app-video-thumbnail [videoId]="v.id" [title]="v.title" [imageUrl]="v.thumbnailUrl" [category]="v.category" [seconds]="v.durationSeconds" />
      </a>
      <div class="flex gap-3 px-1 pt-3 pb-1">
        @if (!hideChannel()) {
          @if (v.ownerId) {
            <a [routerLink]="['/channel', v.ownerId]" class="h-fit rounded-[38%] transition hover:ring-2 hover:ring-accent/50" [attr.aria-label]="v.channelName">
              <app-channel-avatar [name]="v.channelName" [size]="34" />
            </a>
          } @else {
            <app-channel-avatar [name]="v.channelName" [size]="34" />
          }
        }
        <div class="min-w-0">
          <a [routerLink]="['/watch', v.id]">
            <h3 class="line-clamp-2 leading-snug font-semibold transition group-hover:text-accent">{{ v.title }}</h3>
          </a>
          @if (!hideChannel()) {
            @if (v.ownerId) {
              <a [routerLink]="['/channel', v.ownerId]" class="mt-1 block text-sm text-muted transition hover:text-fg">{{ v.channelName }}</a>
            } @else {
              <p class="mt-1 text-sm text-muted">{{ v.channelName }}</p>
            }
          }
          <p class="text-xs text-muted">{{ meta() }}</p>
        </div>
      </div>
    </div>
  `,
})
export class VideoCard {
  readonly video = input.required<Video>();
  /** On a channel page every card is by the same creator, so the creator line is redundant. */
  readonly hideChannel = input(false);

  protected readonly meta = computed(
    () => `${countLabel(this.video().views, 'view')} · ${timeAgo(this.video().publishedAtUtc)}`,
  );
}

import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Video } from '../core/videos/video.service';
import { ChannelAvatar } from './channel-avatar';
import { compactNumber, timeAgo } from './format';
import { VideoThumbnail } from './video-thumbnail';

@Component({
  selector: 'app-video-card',
  imports: [RouterLink, VideoThumbnail, ChannelAvatar],
  template: `
    @let v = video();
    <a
      [routerLink]="['/watch', v.id]"
      class="glass glass-interactive group block rounded-3xl p-2"
    >
      <app-video-thumbnail [videoId]="v.id" [title]="v.title" [category]="v.category" [seconds]="v.durationSeconds" />
      <div class="flex gap-3 px-1 pt-3 pb-1">
        <app-channel-avatar [name]="v.channelName" [size]="34" />
        <div class="min-w-0">
          <h3 class="line-clamp-2 leading-snug font-semibold transition group-hover:text-accent">{{ v.title }}</h3>
          <p class="mt-1 text-sm text-muted">{{ v.channelName }}</p>
          <p class="text-xs text-muted">{{ meta() }}</p>
        </div>
      </div>
    </a>
  `,
})
export class VideoCard {
  readonly video = input.required<Video>();
  protected readonly meta = computed(
    () => `${compactNumber(this.video().views)} views · ${timeAgo(this.video().publishedAtUtc)}`,
  );
}

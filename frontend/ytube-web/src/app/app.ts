import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: `
    <!-- Blurred, slowly drifting gradient behind every page (colors follow the theme) -->
    <div class="aurora" aria-hidden="true"><span></span><span></span><span></span></div>
    <router-outlet />
  `,
})
export class App {}

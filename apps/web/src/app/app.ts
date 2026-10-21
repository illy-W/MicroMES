import { ChangeDetectionStrategy, Component } from '@angular/core';
import { HlmToaster } from '@spartan-ng/helm/sonner';
import { RouterOutlet } from '@angular/router';

@Component({
  imports: [RouterOutlet, HlmToaster],
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}

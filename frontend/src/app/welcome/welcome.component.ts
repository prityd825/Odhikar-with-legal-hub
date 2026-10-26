import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { LanguageService } from '../core/language.service';

@Component({
  selector: 'app-welcome',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './welcome.component.html',
  styleUrls: ['./welcome.component.css']
})
export class WelcomeComponent {
  public langService = inject(LanguageService);
  private router = inject(Router);

  navigateTo(path: string): void {
    this.router.navigate([path]);
  }
}


import { Injectable, signal, computed } from '@angular/core';

export type Language = 'bn' | 'en';

@Injectable({
  providedIn: 'root'
})
export class LanguageService {
  // Stored language preference or default to 'en' if preferred, or 'bn'
  private readonly STORAGE_KEY = 'odhikar_portal_lang';
  
  readonly currentLang = signal<Language>(this.getInitialLang());

  readonly isEnglish = computed(() => this.currentLang() === 'en');
  readonly isBangla = computed(() => this.currentLang() === 'bn');

  constructor() {}

  private getInitialLang(): Language {
    try {
      const saved = localStorage.getItem(this.STORAGE_KEY);
      if (saved === 'en' || saved === 'bn') {
        return saved;
      }
    } catch {}
    return 'bn'; // Default to Bangla with instant 1-click toggle to English
  }

  setLang(lang: Language): void {
    this.currentLang.set(lang);
    try {
      localStorage.setItem(this.STORAGE_KEY, lang);
      document.documentElement.lang = lang;
    } catch {}
  }

  toggleLang(): void {
    this.setLang(this.currentLang() === 'bn' ? 'en' : 'bn');
  }

  /**
   * Helper to return text based on current language
   */
  t(bnText: string, enText: string): string {
    return this.currentLang() === 'en' ? enText : bnText;
  }
}

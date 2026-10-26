import { Injectable, signal, computed } from '@angular/core';

export type Language = 'both' | 'bn' | 'en';

@Injectable({
  providedIn: 'root'
})
export class LanguageService {
  private readonly STORAGE_KEY = 'odhikar_portal_lang_mode_v2';
  
  // Default to 'both' so users immediately see BOTH Bangla and English
  readonly currentLang = signal<Language>(this.getInitialLang());

  readonly isBoth = computed(() => this.currentLang() === 'both');
  readonly isEnglish = computed(() => this.currentLang() === 'en');
  readonly isBangla = computed(() => this.currentLang() === 'bn');

  constructor() {
    this.updateHtmlLang(this.currentLang());
  }

  private getInitialLang(): Language {
    try {
      if (typeof window !== 'undefined' && window.localStorage) {
        const saved = localStorage.getItem(this.STORAGE_KEY) as Language | null;
        if (saved === 'both' || saved === 'en' || saved === 'bn') {
          return saved;
        }
        // If user explicitly selected English previously, keep it, otherwise default to 'both'
        const legacy = localStorage.getItem('odhikar_portal_lang');
        if (legacy === 'en') {
          return 'en';
        }
      }
    } catch {}
    return 'both'; // Default to BOTH so users see both English and Bangla side-by-side
  }

  setLang(lang: Language): void {
    this.currentLang.set(lang);
    try {
      if (typeof window !== 'undefined' && window.localStorage) {
        localStorage.setItem(this.STORAGE_KEY, lang);
        localStorage.setItem('odhikar_portal_lang', lang);
      }
      this.updateHtmlLang(lang);
    } catch {}
  }

  private updateHtmlLang(lang: Language): void {
    if (typeof document !== 'undefined') {
      document.documentElement.lang = lang === 'en' ? 'en' : 'bn';
    }
  }

  /**
   * Helper to return text based on current language mode.
   * - 'en' -> enText
   * - 'bn' -> bnText
   * - 'both' -> bnText + delimiter + enText
   */
  t(bnText: string, enText: string, delimiter = ' · '): string {
    const mode = this.currentLang();
    if (mode === 'en') return enText;
    if (mode === 'bn') return bnText;
    // 'both' mode
    if (!bnText) return enText;
    if (!enText) return bnText;
    return `${bnText}${delimiter}${enText}`;
  }

  tSlash(bnText: string, enText: string): string {
    return this.t(bnText, enText, ' / ');
  }
}

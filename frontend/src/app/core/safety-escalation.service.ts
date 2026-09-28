import { Injectable, signal } from '@angular/core';
import { Subject } from 'rxjs';
import { SpeechService } from './speech.service';

export const DANGER_SIGNALS: string[] = [
  'মেরে ফেলব', 'মেরে ফেলবে', 'খুন করবে', 'খুন করে', 'হত্যা করবে', 'রক্ত', 'মারধর',
  'মারবে', 'গলা টিপে', 'আত্মহত্যা', 'জানে মেরে', 'ছুরি', 'আগুন দিয়ে', 'অ্যাসিড',
  'বিষ খাইয়ে', 'বিষ', 'হাত ভেঙে', 'পা ভেঙে', 'বাঁচাও', 'বাঁচান', 'দা নিয়ে',
  'অস্ত্র', 'তাড়িয়ে দিচ্ছে', 'মারতে আসছে', 'ভয় পাচ্ছি', 'ভয়ে আছি', 'বিপদে আছি',
  'বিপদ', 'মারছে', 'মারধর করছে', 'হুমকি দিচ্ছে'
];

@Injectable({
  providedIn: 'root'
})
export class SafetyEscalationService {
  /**
   * Emits when the safety alert modal is dismissed/closed so consumers can refresh forms & questions
   */
  readonly onDismiss$ = new Subject<void>();

  readonly isEscalated = signal<boolean>(false);
  readonly isDismissed = signal<boolean>(false);
  readonly safetyTitle = signal<string>('জরুরি নিরাপত্তা সতর্কতা');
  readonly safetyMessage = signal<string>('আপনি এখনই বিপদে আছেন বলে মনে হচ্ছে।');
  readonly safetyReason = signal<string | null>(null);

  // Helplines
  readonly helplineNumber = signal<string>('১৬৪৩০');
  readonly emergencyNumber = signal<string>('৯৯৯');
  readonly womenChildHelpline = signal<string>('১০৯');
  readonly clinicName = signal<string>('আইন সহায়তা কেন্দ্র');
  readonly clinicPhone = signal<string>('০১৭০৩-৫৬৯৯৮৮');

  // Voice playback state
  readonly isVoicePlaying = signal<boolean>(false);

  constructor(private speechService: SpeechService) {}

  /**
   * Scans text for critical danger phrases
   */
  detectDanger(text: string): string | null {
    if (!text) return null;
    const lower = text.toLowerCase();
    for (const signal of DANGER_SIGNALS) {
      if (lower.includes(signal)) {
        return signal;
      }
    }
    return null;
  }

  /**
   * Triggers emergency break-out on any screen
   */
  triggerEscalation(reason?: string, clinicPhone?: string, clinicName?: string): void {
    this.isEscalated.set(true);
    this.isDismissed.set(false);

    if (reason) {
      this.safetyReason.set(reason);
    }
    if (clinicPhone) {
      this.clinicPhone.set(clinicPhone);
    }
    if (clinicName) {
      this.clinicName.set(clinicName);
    }

    // Stop current speech or recording
    this.speechService.stopSpeaking();

    // Voice announcement immediately
    this.speakEmergencyNotice();
  }

  /**
   * Speaks the emergency safety notice out loud
   */
  speakEmergencyNotice(): void {
    this.speechService.stopSpeaking();
    this.isVoicePlaying.set(true);

    const spokenText = 'সতর্কতা! আপনি এখনই বিপদে আছেন বলে মনে হচ্ছে। অনুগ্রহ করে অবিলম্বে জাতীয় হেল্পলাইন ১৬৪৩০ অথবা জরুরি সেবা ৯৯৯ নম্বরে এখনই কল করুন। আপনার সুরক্ষা সর্বাধিক গুরুত্বপূর্ণ।';

    this.speechService.speak(spokenText);

    setTimeout(() => {
      this.isVoicePlaying.set(false);
    }, 8000);
  }

  /**
   * Stops the voice announcement
   */
  stopSpeaking(): void {
    this.speechService.stopSpeaking();
    this.isVoicePlaying.set(false);
  }

  /**
   * Closes the popup modal and triggers reset of forms and questions
   */
  dismissModal(): void {
    this.isDismissed.set(true);
    this.isEscalated.set(false);
    this.safetyReason.set(null);
    this.stopSpeaking();
    this.onDismiss$.next();
  }

  /**
   * Re-opens the breakout modal if safety is active
   */
  reopenModal(): void {
    if (this.isEscalated()) {
      this.isDismissed.set(false);
      this.speakEmergencyNotice();
    }
  }

  /**
   * Immediate Break-Out / Safe Exit for domestic violence victims
   * Instantly replaces the page with a neutral website for personal safety
   */
  quickSafeExit(): void {
    this.stopSpeaking();
    if (typeof window !== 'undefined') {
      window.location.replace('https://www.google.com');
    }
  }

  /**
   * Reset safety state (e.g. when starting a new session)
   */
  reset(): void {
    this.stopSpeaking();
    this.isEscalated.set(false);
    this.isDismissed.set(false);
    this.safetyReason.set(null);
  }
}

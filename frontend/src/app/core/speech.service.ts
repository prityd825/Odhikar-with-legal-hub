import { Injectable, signal } from '@angular/core';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class SpeechService {
  // STT Signals
  readonly isListening = signal<boolean>(false);
  readonly isRecording = signal<boolean>(false);
  readonly currentTranscript = signal<string>('');
  readonly interimTranscript = signal<string>('');
  readonly sttError = signal<string | null>(null);
  readonly isSttSupported = signal<boolean>(false);

  // TTS Signals
  readonly isSpeaking = signal<boolean>(false);
  readonly speakingText = signal<string | null>(null);

  private recognition: any = null;
  private banglaVoice: SpeechSynthesisVoice | null = null;
  private onFinalCallback: ((text: string) => void) | null = null;
  private onInterimCallback: ((text: string) => void) | null = null;

  private activeAudio: HTMLAudioElement | null = null;
  private currentUtterance: SpeechSynthesisUtterance | null = null;

  constructor() {
    if (typeof window !== 'undefined') {
      this.initStt();
      this.initTts();
    }
  }

  // =========================================================================
  // Native Browser STT (SpeechRecognition / webkitSpeechRecognition)
  // =========================================================================
  private initStt(): void {
    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;
    if (!SpeechRecognition) {
      this.isSttSupported.set(false);
      return;
    }

    this.isSttSupported.set(true);

    try {
      this.recognition = new SpeechRecognition();
      this.recognition.lang = 'bn-BD';
      this.recognition.continuous = true;
      this.recognition.interimResults = true;
      this.recognition.maxAlternatives = 1;

      this.recognition.onstart = () => {
        this.isListening.set(true);
        this.sttError.set(null);
      };

      this.recognition.onresult = (event: any) => {
        let interim = '';
        let final = '';

        for (let i = event.resultIndex; i < event.results.length; ++i) {
          const res = event.results[i];
          const text = res[0].transcript;
          if (res.isFinal) {
            final += text;
          } else {
            interim += text;
          }
        }

        if (interim) {
          this.interimTranscript.set(interim);
          const current = this.currentTranscript();
          const preview = current ? `${current} ${interim.trim()}` : interim.trim();
          this.onInterimCallback?.(preview);
        }

        if (final) {
          const current = this.currentTranscript();
          const combined = current ? `${current} ${final.trim()}` : final.trim();
          this.currentTranscript.set(combined);
          this.interimTranscript.set('');
          this.onFinalCallback?.(combined);
        }
      };

      this.recognition.onerror = (event: any) => {
        console.warn('SpeechRecognition error:', event.error);
        if (event.error === 'not-allowed') {
          this.sttError.set('মাইক্রোফোন ব্যবহারের অনুমতি পাওয়া যায়নি। ব্রাউজারে অনুমতি প্রদান করুন।');
        } else if (event.error === 'no-speech') {
          // Normal when silent
        } else if (event.error === 'audio-capture') {
          this.sttError.set('কোনো মাইক্রোফোন ডিভাইস সনাক্ত করা যায়নি।');
        } else {
          this.sttError.set(`ভয়েস রিকগনিশন ত্রুটি: ${event.error}`);
        }
        this.isListening.set(false);
      };

      this.recognition.onend = () => {
        this.isListening.set(false);
        this.interimTranscript.set('');
      };
    } catch (err) {
      console.error('Failed to initialize SpeechRecognition:', err);
      this.isSttSupported.set(false);
    }
  }

  startListening(
    onFinal?: (text: string) => void,
    onInterim?: (text: string) => void,
    initialText: string = ''
  ): void {
    if (!this.recognition) {
      this.initStt();
      if (!this.recognition) {
        this.sttError.set('আপনার ব্রাউজার সরাসরি Web SpeechRecognition সাপোর্ট করে না। ক্রোম বা এজ ব্যবহার করুন।');
        return;
      }
    }

    this.onFinalCallback = onFinal || null;
    this.onInterimCallback = onInterim || null;
    this.sttError.set(null);
    this.interimTranscript.set('');
    this.currentTranscript.set(initialText ? initialText.trim() : '');

    try {
      this.recognition.start();
    } catch (err: any) {
      try {
        this.recognition.stop();
        setTimeout(() => this.recognition?.start(), 150);
      } catch {
        // ignore
      }
    }
  }

  stopListening(): void {
    if (this.recognition && this.isListening()) {
      try {
        this.recognition.stop();
      } catch {
        // ignore
      }
    }
    this.isListening.set(false);
  }

  toggleListening(
    onFinal?: (text: string) => void,
    onInterim?: (text: string) => void
  ): void {
    if (this.isListening()) {
      this.stopListening();
    } else {
      this.startListening(onFinal, onInterim);
    }
  }

  // =========================================================================
  // MediaRecorder for Multimodal Audio Uploads
  // =========================================================================
  async getMediaRecorder(): Promise<MediaRecorder | null> {
    if (!navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === 'undefined') {
      return null;
    }
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const mimeTypes = ['audio/webm;codecs=opus', 'audio/webm', 'audio/ogg', 'audio/mp4'];
      const supportedType = mimeTypes.find(type => MediaRecorder.isTypeSupported(type)) || '';
      return supportedType ? new MediaRecorder(stream, { mimeType: supportedType }) : new MediaRecorder(stream);
    } catch {
      return null;
    }
  }

  // =========================================================================
  // TTS (Dual Engine: Backend High-Fidelity Bangla Audio + Browser Fallback)
  // =========================================================================
  private initTts(): void {
    if (!('speechSynthesis' in window)) return;

    const pickVoice = () => {
      const voices = window.speechSynthesis.getVoices();
      this.banglaVoice =
        voices.find(v => v.lang === 'bn-BD') ||
        voices.find(v => v.lang === 'bn-IN') ||
        voices.find(v => v.lang.toLowerCase().includes('bn')) ||
        null;
    };

    pickVoice();
    if (window.speechSynthesis.onvoiceschanged !== undefined) {
      window.speechSynthesis.onvoiceschanged = pickVoice;
    }
  }

  isSpeakingText(text: string): boolean {
    return this.isSpeaking() && this.speakingText() === text;
  }

  toggleSpeak(text: string, rate: number = 0.9): void {
    if (this.isSpeakingText(text)) {
      this.stopSpeaking();
    } else {
      this.speak(text, rate);
    }
  }

  speak(text: string, rate: number = 0.9, onEnd?: () => void): void {
    if (typeof window === 'undefined' || !text?.trim()) {
      onEnd?.();
      return;
    }

    this.stopSpeaking();

    const cleanText = text.replace(/[#*_]/g, ' ').replace(/\s+/g, ' ').trim();
    this.isSpeaking.set(true);
    this.speakingText.set(text);

    // 1. High-fidelity Bangla speech synthesis via backend TTS endpoint
    try {
      const audioUrl = `${environment.apiUrl}/intake/tts?q=${encodeURIComponent(cleanText)}`;
      const audio = new Audio(audioUrl);
      this.activeAudio = audio;

      audio.onended = () => {
        if (this.activeAudio === audio) {
          this.isSpeaking.set(false);
          this.speakingText.set(null);
          this.activeAudio = null;
          onEnd?.();
        }
      };

      audio.onerror = () => {
        // Fallback to browser SpeechSynthesis if network or audio element fails
        if (this.activeAudio === audio) {
          this.activeAudio = null;
          this.fallbackBrowserTts(cleanText, text, rate, onEnd);
        }
      };

      const playPromise = audio.play();
      if (playPromise !== undefined) {
        playPromise.catch((err) => {
          console.warn('HTML5 Audio play warning, attempting fallback:', err);
          if (this.activeAudio === audio) {
            this.activeAudio = null;
            this.fallbackBrowserTts(cleanText, text, rate, onEnd);
          }
        });
      }
      return;
    } catch {
      this.fallbackBrowserTts(cleanText, text, rate, onEnd);
    }
  }

  private fallbackBrowserTts(cleanText: string, originalText: string, rate: number = 0.9, onEnd?: () => void): void {
    if (!('speechSynthesis' in window)) {
      this.isSpeaking.set(false);
      this.speakingText.set(null);
      onEnd?.();
      return;
    }

    if (window.speechSynthesis.paused) {
      window.speechSynthesis.resume();
    }

    const utterance = new SpeechSynthesisUtterance(cleanText);
    utterance.rate = rate;
    utterance.pitch = 1.0;

    const allVoices = window.speechSynthesis.getVoices();
    const voice =
      this.banglaVoice ||
      allVoices.find(v => v.lang === 'bn-BD') ||
      allVoices.find(v => v.lang === 'bn-IN') ||
      allVoices.find(v => v.lang.toLowerCase().includes('bn')) ||
      allVoices.find(v => v.name.toLowerCase().includes('bangla') || v.name.toLowerCase().includes('bengali')) ||
      null;

    if (voice) {
      utterance.voice = voice;
      utterance.lang = voice.lang;
    } else {
      utterance.lang = 'bn-BD';
    }

    this.currentUtterance = utterance;

    utterance.onstart = () => {
      this.isSpeaking.set(true);
      this.speakingText.set(originalText);
    };

    utterance.onend = () => {
      if (this.currentUtterance === utterance) {
        this.isSpeaking.set(false);
        this.speakingText.set(null);
        this.currentUtterance = null;
        onEnd?.();
      }
    };

    utterance.onerror = (e) => {
      console.warn('SpeechSynthesis error:', e);
      if (this.currentUtterance === utterance) {
        this.isSpeaking.set(false);
        this.speakingText.set(null);
        this.currentUtterance = null;
        onEnd?.();
      }
    };

    window.speechSynthesis.speak(utterance);
  }

  stopSpeaking(): void {
    if (this.activeAudio) {
      try {
        this.activeAudio.pause();
        this.activeAudio.currentTime = 0;
      } catch {
        // ignore
      }
      this.activeAudio = null;
    }

    if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
      try {
        window.speechSynthesis.cancel();
      } catch {
        // ignore
      }
      this.currentUtterance = null;
    }

    this.isSpeaking.set(false);
    this.speakingText.set(null);
  }
}
import { Component, inject, signal, computed, effect, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { ApiService } from '../core/api.service';
import { SpeechService } from '../core/speech.service';
import { SafetyEscalationService } from '../core/safety-escalation.service';
import {
  ExtractedLegalData,
  ChatResponseDto,
  GeneratedCaseSummaryDto,
  GeneratedApplicationDto,
  AlertInformationDto,
  LegalAidClinic
} from '../core/intake.models';
import { INCIDENTS, IncidentDefinition, RULES } from '../core/legal';

export interface AnswerRecord {
  step: number;
  question: string;
  answer: string;
  isVoice?: boolean;
}

@Component({
  selector: 'app-intake',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './intake.component.html',
  styles: ``
})
export class IntakeComponent implements OnInit, OnDestroy {
  readonly safetyService = inject(SafetyEscalationService);
  private api = inject(ApiService);
  public speechService = inject(SpeechService);
  private router = inject(Router);
  private safetyDismissSub?: Subscription;

  // Dynamic Parent Incident Categories (initialized with canonical fallback)
  readonly incidents = signal<IncidentDefinition[]>(INCIDENTS);

  // Selected Category
  readonly selectedCode = signal<string | null>(null);
  readonly selectedIncident = signal<string | null>(null);

  // Clinics from DB & Referral
  readonly clinics = signal<LegalAidClinic[]>([]);
  readonly selectedClinic = signal<LegalAidClinic | null>(null);
  readonly isReferralConfirmed = signal<boolean>(false);

  // Voice Tour & Illiterate Support Signals
  readonly isSpeakingIncidentTour = signal<boolean>(false);
  readonly highlightedIncidentCode = signal<string | null>(null);
  readonly incidentSelectionRequired = signal<boolean>(false);
  private tourTimeoutId: any = null;

  readonly incidentTourSteps: { code: string | null; text: string }[] = [
    {
      code: null,
      text: 'কোন বিষয়ে সহায়তা চান? নিচে ছয়টি বিষয় রয়েছে। শুনুন এবং আপনার অভিযোগের বিষয়টি বেছে নিতে কার্ডে চাপ দিন।'
    },
    {
      code: 'DOWER_MAINTENANCE',
      text: 'এক: দেনমোহর বা ভরণপোষণ।'
    },
    {
      code: 'DOWRY_DOMESTIC_VIOLENCE',
      text: 'দুই: যৌতুক বা পারিবারিক সহিংসতা।'
    },
    {
      code: 'LABOUR_WAGE',
      text: 'তিন: বকেয়া মজুরি বা শ্রম।'
    },
    {
      code: 'LAND_PROPERTY',
      text: 'চার: জমি বা সম্পত্তি বিরোধ।'
    },
    {
      code: 'WOMEN_CHILD_PROTECTION',
      text: 'পাঁচ: নারী ও শিশু সুরক্ষা।'
    },
    {
      code: 'OTHER',
      text: 'ছয়: অন্য সমস্যা।'
    },
    {
      code: null,
      text: 'আপনার পছন্দের বিষয়ের কার্ডটিতে চাপ দিন।'
    }
  ];

  // Step state
  readonly currentStep = signal<number>(1);
  readonly answeredCount = signal<number>(0);
  readonly currentQuestion = signal<string>('আপনার সমস্যাটি নিজের ভাষায় বলুন।');
  readonly inputText = signal<string>('');
  readonly answersHistory = signal<AnswerRecord[]>([]);
  readonly showHistory = signal<boolean>(false);

  // Status & Backend AI Signals
  readonly conversationId = signal<string | null>(null);
  readonly caseId = signal<string | null>(null);
  readonly caseNumber = signal<string | null>(null);
  readonly isLoading = signal<boolean>(false);
  readonly isGeneratingDraft = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly isSafetyEscalated = signal<boolean>(false);
  readonly safetyReason = signal<string | null>(null);
  readonly alertInformation = signal<AlertInformationDto | null>(null);
  readonly isComplete = signal<boolean>(false);
  readonly extractedData = signal<ExtractedLegalData | null>(null);
  readonly category = signal<string | null>(null);

  // Generated Case Summary & Draft Application
  readonly generatedCaseSummary = signal<GeneratedCaseSummaryDto | null>(null);
  readonly generatedApplication = signal<GeneratedApplicationDto | null>(null);

  // Auto TTS toggler
  readonly autoTtsEnabled = signal<boolean>(true);
  readonly totalQuestions = signal<number | null>(null);

  toBengaliNumber(num: number): string {
    const bengaliDigits = ['০', '১', '২', '৩', '৪', '৫', '৬', '৭', '৮', '৯'];
    return num.toString().split('').map(d => {
      const digit = parseInt(d, 10);
      return isNaN(digit) ? d : bengaliDigits[digit];
    }).join('');
  }

  // Computed Bengali Step Number & Counters
  readonly bengaliStep = computed(() => {
    return this.toBengaliNumber(this.currentStep());
  });

  readonly bengaliAnsweredCount = computed(() => {
    return this.toBengaliNumber(this.answeredCount());
  });

  readonly bengaliTotalQuestions = computed(() => {
    const total = this.totalQuestions();
    return total != null && total > 0 ? this.toBengaliNumber(total) : null;
  });

  // Dynamic Legal Right statement
  readonly currentLegalRight = computed(() => {
    const item = this.incidents().find(i => i.code === this.selectedCode());
    return item?.rightShort || 'দেনমোহর একটি আইনত আদায়যোগ্য পাওনা।';
  });

  readonly currentIncidentRight = computed(() => {
    return this.currentLegalRight();
  });

  readonly currentActionSteps = computed(() => {
    const code = this.selectedCode();
    const rule = (code ? RULES[code] : null) || RULES['DOWER_MAINTENANCE'] || RULES['OTHER'];
    return rule?.next || [
      'নিকাহনামা ও পরিচয়পত্র প্রস্তুত রাখুন।',
      'নিকটস্থ লিগ্যাল এইড অফিসে যোগাযোগ করুন।'
    ];
  });

  // Multimodal MediaRecorder variables
  private mediaRecorder: MediaRecorder | null = null;
  private audioChunks: Blob[] = [];

  constructor() {
    effect(() => {
      const error = this.speechService.sttError();
      if (error) {
        this.errorMessage.set(error);
      }
    });
  }

  ngOnInit(): void {
    this.loadParentCategories();
    this.loadClinics();

    // When the safety alert popup is closed, immediately refresh all forms & questions
    this.safetyDismissSub = this.safetyService.onDismiss$.subscribe(() => {
      this.resetIntake(true);
      if (typeof window !== 'undefined') {
        window.scrollTo({ top: 0, behavior: 'smooth' });
      }
    });
  }

  ngOnDestroy(): void {
    this.safetyDismissSub?.unsubscribe();
  }

  loadClinics(): void {
    this.api.getClinics().subscribe({
      next: (data) => {
        if (data && data.length > 0) {
          this.clinics.set(data);
          this.selectedClinic.set(data[0]);
        } else {
          this.setFallbackClinic();
        }
      },
      error: () => {
        this.setFallbackClinic();
      }
    });
  }

  setFallbackClinic(): void {
    const fallback: LegalAidClinic = {
      name: 'জেলা লিগ্যাল এইড অফিস',
      nameBn: 'জেলা লিগ্যাল এইড অফিস',
      branchName: 'ঢাকা জজ কোর্ট ভবন শাখা',
      branchBn: 'ঢাকা জজ কোর্ট ভবন শাখা',
      district: 'ঢাকা',
      districtBn: 'ঢাকা',
      schedule: 'বুধবার, সকাল ১০টায়',
      scheduleBn: 'বুধবার, সকাল ১০টায়',
      addressBn: 'জেলা ও দায়রা জজ আদালত প্রাঙ্গণ, ঢাকা',
      phone: '১৬৪৩০'
    };
    this.clinics.set([fallback]);
    this.selectedClinic.set(fallback);
  }

  loadParentCategories(): void {
    this.api.getParentCategories().subscribe({
      next: (categories) => {
        if (categories && categories.length > 0) {
          const mapped: IncidentDefinition[] = categories
            .filter(c => c.isActive)
            .sort((a, b) => a.order - b.order)
            .map(cat => {
              const defaultDef = INCIDENTS.find(i => i.code === cat.code);
              return {
                code: cat.code,
                bn: cat.name || defaultDef?.bn || cat.code,
                en: cat.nameEn || defaultDef?.en || cat.code,
                description: cat.description || defaultDef?.description || '',
                rightShort: defaultDef?.rightShort || 'আইনি সহায়তার জন্য আপনার অধিকার সংরক্ষিত রয়েছে।',
                icon: cat.icon || defaultDef?.icon || 'scale'
              };
            });
          this.incidents.set(mapped);

          // If current selection is in the list, update display labels
          const current = mapped.find(m => m.code === this.selectedCode());
          if (current) {
            this.selectedIncident.set(current.bn);
            this.category.set(current.bn);
          }
        }
      },
      error: (err) => {
        console.warn('Backend parent categories API unavailable, using fallback list', err);
      }
    });
  }

  startIncidentVoiceTour(): void {
    this.stopIncidentVoiceTour();
    this.isSpeakingIncidentTour.set(true);

    let stepIndex = 0;
    const playNext = () => {
      if (!this.isSpeakingIncidentTour()) return;

      if (stepIndex >= this.incidentTourSteps.length) {
        this.stopIncidentVoiceTour();
        return;
      }

      const current = this.incidentTourSteps[stepIndex];
      this.highlightedIncidentCode.set(current.code);

      if (current.code && typeof document !== 'undefined') {
        const el = document.getElementById('incident-' + current.code);
        el?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
      }

      stepIndex++;
      this.speechService.speak(current.text, 0.88, () => {
        if (!this.isSpeakingIncidentTour()) return;
        this.tourTimeoutId = setTimeout(() => {
          playNext();
        }, 350);
      });
    };

    playNext();
  }

  stopIncidentVoiceTour(): void {
    this.isSpeakingIncidentTour.set(false);
    this.highlightedIncidentCode.set(null);
    if (this.tourTimeoutId) {
      clearTimeout(this.tourTimeoutId);
      this.tourTimeoutId = null;
    }
    this.speechService.stopSpeaking();
  }

  toggleIncidentVoiceTour(): void {
    if (this.isSpeakingIncidentTour()) {
      this.stopIncidentVoiceTour();
    } else {
      this.startIncidentVoiceTour();
    }
  }

  speakSingleIncident(item: IncidentDefinition, event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    this.stopIncidentVoiceTour();
    this.highlightedIncidentCode.set(item.code);
    const text = `${item.bn}। ${item.description}। নির্বাচন করতে কার্ডে চাপ দিন।`;
    this.speechService.speak(text, 0.9, () => {
      if (this.highlightedIncidentCode() === item.code) {
        this.highlightedIncidentCode.set(null);
      }
    });
  }

  requireIncidentSelectionWithVoice(): void {
    this.incidentSelectionRequired.set(true);
    if (typeof document !== 'undefined') {
      const section = document.getElementById('incident-selection-section');
      section?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    const warningSpeech = 'কথা বলার আগে অনুগ্রহ করে উপরের তালিকা থেকে আপনার সমস্যার বিষয়টি বেছে নিন। অপশনগুলো শুনতে "শুনুন" বাটনে চাপ দিন।';
    this.speechService.speak(warningSpeech, 0.9, () => {
      if (!this.selectedCode()) {
        this.startIncidentVoiceTour();
      }
    });
  }

  selectIncident(item: IncidentDefinition): void {
    if (this.answeredCount() > 0 && !confirm('নতুন বিষয় নির্বাচন করলে পূর্বের উত্তরগুলো রিসেট হবে। আপনি কি পরিবর্তন করতে চান?')) {
      return;
    }
    this.stopIncidentVoiceTour();
    this.selectedCode.set(item.code);
    this.selectedIncident.set(item.bn);
    this.category.set(item.bn);
    this.incidentSelectionRequired.set(false);

    // Update clinic matching selection
    const clinic = this.clinics()[0] || null;
    if (clinic) {
      this.selectedClinic.set(clinic);
    }

    // Spoken voice feedback confirming selection for illiterate user
    const confirmNotice = `আপনি নির্বাচন করেছেন: ${item.bn}। এবার নিচে আপনার সমস্যার কথা বলুন।`;
    this.speechService.speak(confirmNotice, 0.95);

    this.resetIntake(false);
  }

  speakCurrentQuestion(): void {
    const q = !this.selectedCode() ? "কথা বলার আগে অনুগ্রহ করে উপরের তালিকা থেকে আপনার সমস্যার বিষয়টি বেছে নিন।" : this.currentQuestion();
    if (this.speechService.isSpeakingText(q)) {
      this.speechService.stopSpeaking();
    } else {
      this.speechService.speak(q);
    }
  }

  speakCurrentRight(): void {
    const right = this.currentLegalRight();
    if (this.speechService.isSpeakingText(right)) {
      this.speechService.stopSpeaking();
    } else {
      this.speechService.speak(right);
    }
  }

  toggleAutoTts(): void {
    const nextVal = !this.autoTtsEnabled();
    this.autoTtsEnabled.set(nextVal);
    if (!nextVal) {
      this.speechService.stopSpeaking();
    }
  }

  onEnterPress(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      if (!this.selectedCode()) {
        this.requireIncidentSelectionWithVoice();
        return;
      }
      this.submitAnswer();
    }
  }

  toggleVoice(): void {
    if (this.isLoading() || this.isSafetyEscalated() || this.isComplete()) return;

    // Requirement: User must select incident type before giving voice
    if (!this.selectedCode()) {
      this.requireIncidentSelectionWithVoice();
      return;
    }

    if (this.speechService.isRecording() || this.speechService.isListening()) {
      this.stopVoiceRecording();
    } else {
      this.startVoiceRecording();
    }
  }

  private async startVoiceRecording(): Promise<void> {
    this.speechService.stopSpeaking();
    this.errorMessage.set(null);

    const initialText = this.inputText().trim();
    const checkForDanger = (spoken: string) => {
      const dangerSignal = this.safetyService.detectDanger(spoken);
      if (dangerSignal) {
        this.stopVoiceRecording();
        this.isSafetyEscalated.set(true);
        const clinic = this.selectedClinic();
        const clinicPhone = clinic?.phone || clinic?.contactNumber || '০১৭০৩-৫৬৯৯৮৮';
        const clinicName = clinic?.nameBn || clinic?.name || 'আইন সহায়তা কেন্দ্র';
        this.safetyService.triggerEscalation(
          `কথোপকথনে সম্ভাব্য সহিংসতা ও তাৎক্ষণিক বিপদের ঝুঁকি সনাক্ত হয়েছে ("${dangerSignal}")।`,
          clinicPhone,
          clinicName
        );
      }
    };

    this.speechService.startListening(
      (finalText) => {
        if (finalText) {
          this.inputText.set(finalText);
          checkForDanger(finalText);
        }
      },
      (interim) => {
        if (interim) {
          this.inputText.set(interim);
          checkForDanger(interim);
        }
      },
      initialText
    );

    const recorder = await this.speechService.getMediaRecorder();
    if (recorder) {
      this.mediaRecorder = recorder;
      this.audioChunks = [];

      recorder.ondataavailable = (e: BlobEvent) => {
        if (e.data && e.data.size > 0) {
          this.audioChunks.push(e.data);
        }
      };

      recorder.onstop = () => {
        if (this.audioChunks.length > 0) {
          const mimeType = recorder.mimeType || 'audio/webm';
          const audioBlob = new Blob(this.audioChunks, { type: mimeType });
          this.audioChunks = [];
          this.submitVoiceAnswer(audioBlob);
        }
      };

      try {
        recorder.start(250);
        this.speechService.isRecording.set(true);
      } catch (err) {
        console.error('Failed to start MediaRecorder:', err);
      }
    }
  }

  private stopVoiceRecording(): void {
    this.speechService.stopListening();
    if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
      try {
        this.mediaRecorder.stop();
      } catch {
        // ignore
      }
    }
    this.speechService.isRecording.set(false);
  }

  skipOrDontKnow(): void {
    if (!this.selectedCode()) {
      this.requireIncidentSelectionWithVoice();
      return;
    }
    this.submitAnswer('জানা নেই');
  }

  submitAnswer(overrideText?: string): void {
    if (!this.selectedCode()) {
      this.requireIncidentSelectionWithVoice();
      return;
    }
    const text = (overrideText !== undefined ? overrideText : this.inputText()).trim();
    if (!text || this.isLoading() || this.isSafetyEscalated() || this.isComplete()) {
      return;
    }

    // Proactive safety danger check on typed input
    const dangerSignal = this.safetyService.detectDanger(text);
    if (dangerSignal) {
      this.stopVoiceRecording();
      this.isSafetyEscalated.set(true);
      const clinic = this.selectedClinic();
      const clinicPhone = clinic?.phone || clinic?.contactNumber || '০১৭০৩-৫৬৯৯৮৮';
      const clinicName = clinic?.nameBn || clinic?.name || 'আইন সহায়তা কেন্দ্র';
      this.safetyService.triggerEscalation(
        `কথোপকথনে সম্ভাব্য সহিংসতা ও তাৎক্ষণিক বিপদের ঝুঁকি সনাক্ত হয়েছে ("${dangerSignal}")।`,
        clinicPhone,
        clinicName
      );
      return;
    }

    this.stopVoiceRecording();
    this.speechService.stopSpeaking();
    this.errorMessage.set(null);

    const questionAsked = this.currentQuestion();
    const currentStepNum = this.currentStep();

    const formData = new FormData();
    formData.append('text', text);
    formData.append('incidentType', this.selectedIncident() || 'সাধারণ আইনি সহায়তা');
    formData.append('categoryCode', this.selectedCode() || 'OTHER');
    if (this.conversationId()) {
      formData.append('conversationId', this.conversationId()!);
    }

    this.isLoading.set(true);

    this.api.sendChatMessage(formData).subscribe({
      next: (response: ChatResponseDto) => {
        this.isLoading.set(false);
        this.conversationId.set(response.conversationId);

        this.answersHistory.update(prev => [
          ...prev,
          { step: currentStepNum, question: questionAsked, answer: text, isVoice: false }
        ]);
        this.inputText.set('');

        this.processAiResponse(response);
      },
      error: (err) => {
        this.isLoading.set(false);
        console.error('Submit answer error:', err);
        const detail = err.error?.message || err.message || 'সার্ভারের সাথে সংযোগে সমস্যা হয়েছে। আবার চেষ্টা করুন।';
        this.errorMessage.set(detail);
      }
    });
  }

  submitVoiceAnswer(audioBlob: Blob): void {
    if (this.isLoading() || this.isSafetyEscalated() || this.isComplete()) return;

    this.stopVoiceRecording();
    this.speechService.stopSpeaking();
    this.errorMessage.set(null);

    const questionAsked = this.currentQuestion();
    const currentStepNum = this.currentStep();
    const typedText = this.inputText().trim();

    const formData = new FormData();
    formData.append('audio', audioBlob, 'record.webm');
    if (typedText) {
      formData.append('text', typedText);
    }
    formData.append('incidentType', this.selectedIncident() || 'সাধারণ আইনি সহায়তা');
    formData.append('categoryCode', this.selectedCode() || 'OTHER');
    if (this.conversationId()) {
      formData.append('conversationId', this.conversationId()!);
    }

    this.isLoading.set(true);

    this.api.sendChatMessage(formData).subscribe({
      next: (response: ChatResponseDto) => {
        this.isLoading.set(false);
        this.conversationId.set(response.conversationId);

        const transcript = response.userTranscript?.trim() || typedText || 'ভয়েস বার্তা';

        this.answersHistory.update(prev => [
          ...prev,
          { step: currentStepNum, question: questionAsked, answer: transcript, isVoice: true }
        ]);
        this.inputText.set('');

        this.processAiResponse(response);
      },
      error: (err) => {
        this.isLoading.set(false);
        console.error('Submit voice answer error:', err);
        const detail = err.error?.message || err.message || 'ভয়েস প্রক্রিয়াকরণে সমস্যা হয়েছে। আবার চেষ্টা করুন।';
        this.errorMessage.set(detail);
      }
    });
  }

  retryFetchDrafts(): void {
    const convId = this.conversationId();
    if (!convId) return;

    this.isGeneratingDraft.set(true);
    this.errorMessage.set(null);

    this.api.generateCaseSummary(convId).subscribe({
      next: (summary) => {
        this.generatedCaseSummary.set(summary);
        this.api.generateApplicationDraft(convId).subscribe({
          next: (appDraft) => {
            this.generatedApplication.set(appDraft);
            this.isGeneratingDraft.set(false);
          },
          error: (appErr) => {
            console.warn('App draft generation error:', appErr);
            this.isGeneratingDraft.set(false);
          }
        });
      },
      error: (sumErr) => {
        console.error('Summary generation error:', sumErr);
        this.isGeneratingDraft.set(false);
        this.errorMessage.set('কেস সামারি প্রস্তুত করতে সমস্যা হয়েছে। অনুগ্রহ করে পুনরায় চেষ্টা করুন।');
      }
    });
  }

  private processAiResponse(response: ChatResponseDto): void {
    if (response.caseId) {
      this.caseId.set(response.caseId);
      if (typeof window !== 'undefined') {
        sessionStorage.setItem('lastSelectedCaseId', response.caseId);
      }
    }
    if (response.caseNumber) {
      this.caseNumber.set(response.caseNumber);
    }
    if (response.extractedData) {
      this.extractedData.set(response.extractedData);
    }
    if (response.category) {
      this.category.set(response.category);
    }

    // Safety Escalation Check - BREAK-OUT Emergency Flow
    if (response.isSafetyEscalated) {
      this.isSafetyEscalated.set(true);
      this.stopVoiceRecording();
      this.speechService.stopSpeaking();

      const reason = response.safetyReason || 'শারীরিক নির্যাতন, আক্রমণ বা জীবননাশের তাৎক্ষণিক ঝুঁকি সনাক্ত হয়েছে।';
      this.safetyReason.set(reason);
      if (response.alertInformation) {
        this.alertInformation.set(response.alertInformation);
      }

      const clinic = this.selectedClinic();
      const clinicPhone = clinic?.phone || clinic?.contactNumber || '০১৭০৩-৫৬৯৯৮৮';
      const clinicName = clinic?.nameBn || clinic?.name || 'আইন সহায়তা কেন্দ্র';

      this.safetyService.triggerEscalation(reason, clinicPhone, clinicName);
      return;
    }

    // Store generated summary & draft application if returned
    if (response.generatedCaseSummary) {
      this.generatedCaseSummary.set(response.generatedCaseSummary);
    }
    if (response.generatedApplication) {
      this.generatedApplication.set(response.generatedApplication);
    }
    if (response.totalQuestions) {
      this.totalQuestions.set(response.totalQuestions);
    }

    // Update answered counter
    const newAnsweredCount = this.answeredCount() + 1;
    this.answeredCount.set(newAnsweredCount);

    const hasFollowUp = !!response.followUpQuestion || (response.isFollowUpQuestion && !!response.assistantMessage);

    // Check Completion dynamically based on backend isComplete flag and presence of next questions
    if (response.isComplete || !hasFollowUp) {
      this.isComplete.set(true);
      if (this.autoTtsEnabled()) {
        setTimeout(() => {
          this.speakRightsAndReferral();
        }, 300);
      }

      // If summary or app draft wasn't included directly, fetch them on demand
      if (!this.generatedCaseSummary() && this.conversationId()) {
        this.retryFetchDrafts();
      }
      return;
    }

    // Advance to Next Question
    const nextQuestion = response.followUpQuestion || response.assistantMessage || 'পরবর্তী বিবরণ বলুন।';
    this.currentStep.set(newAnsweredCount + 1);
    this.currentQuestion.set(nextQuestion);

    if (this.autoTtsEnabled()) {
      setTimeout(() => {
        this.speechService.speak(nextQuestion);
      }, 250);
    }
  }

  speakRightsAndReferral(): void {
    const right = this.currentIncidentRight();
    const steps = this.currentActionSteps().join('। ');
    const clinic = this.selectedClinic();
    const clinicName = clinic?.nameBn || clinic?.name || clinic?.branchBn || 'নিকটতম আইন সহায়তা কেন্দ্র';
    const clinicSchedule = clinic?.scheduleBn || clinic?.schedule || 'বুধবার, সকাল ১০টায়';

    const fullText = `অধিকার: ${right}। সম্ভাব্য পথ: ${steps}। রেফারেল: ${clinicName}, ${clinicSchedule}। আপনার তথ্য আগেই পাঠানো হয়েছে। পুনরায় নতুন আবেদন করতে নিচের সবুজ বাটনে চাপ দিন।`;
    this.speechService.speak(fullText);
  }

  speakNewApplyVoiceGuidance(): void {
    this.speechService.speak('নতুন আবেদন করতে এই সবুজ বাটনে চাপ দিন।');
  }

  startNewApplication(): void {
    this.speechService.speak('নতুন আবেদন শুরু করা হচ্ছে। অনুগ্রহ করে আপনার অভিযোগের বিষয়টি নির্বাচন করুন।', 0.9, () => {
      this.startIncidentVoiceTour();
    });
    this.resetIntake(true);
    if (typeof window !== 'undefined') {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  confirmReferral(): void {
    this.isReferralConfirmed.set(true);
    const clinic = this.selectedClinic();
    const clinicName = clinic?.nameBn || clinic?.name || clinic?.branchBn || 'আইন সহায়তা কেন্দ্র';
    const time = clinic?.scheduleBn || clinic?.schedule || 'বুধবার, সকাল ১০টায়';
    this.speechService.speak(`আপনার রেফারেল সফলভাবে নিশ্চিত করা হয়েছে। ${clinicName}-এ ${time} উপস্থিত থাকার অনুরোধ করা হচ্ছে।`);
  }

  goBack(): void {
    this.isComplete.set(false);
    this.isReferralConfirmed.set(false);
  }

  resetIntake(clearCategory: boolean = true): void {
    this.stopVoiceRecording();
    this.speechService.stopSpeaking();
    this.conversationId.set(null);
    this.caseId.set(null);
    this.caseNumber.set(null);
    this.currentStep.set(1);
    this.answeredCount.set(0);
    this.totalQuestions.set(null);
    this.isReferralConfirmed.set(false);
    this.currentQuestion.set('আপনার সমস্যাটি নিজের ভাষায় বলুন।');
    this.inputText.set('');
    this.answersHistory.set([]);
    this.isSafetyEscalated.set(false);
    this.safetyReason.set(null);
    this.safetyService.reset();
    this.alertInformation.set(null);
    this.isComplete.set(false);
    this.extractedData.set(null);
    this.generatedCaseSummary.set(null);
    this.generatedApplication.set(null);
    this.errorMessage.set(null);
    if (clearCategory) {
      this.selectedCode.set(null);
      this.selectedIncident.set(null);
      this.category.set(null);
    }
  }

  proceedToDraft(): void {
    const qp: Record<string, string> = {};
    if (this.caseId()) qp['caseId'] = this.caseId()!;
    if (this.caseNumber()) qp['caseNumber'] = this.caseNumber()!;
    if (this.conversationId()) qp['conversationId'] = this.conversationId()!;
    this.router.navigate(['/paralegal'], { queryParams: qp });
  }
}

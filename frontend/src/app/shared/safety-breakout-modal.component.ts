import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SafetyEscalationService } from '../core/safety-escalation.service';

@Component({
  selector: 'app-safety-breakout-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (safetyService.isEscalated() && !safetyService.isDismissed()) {
      <div class="safety-backdrop animate-fade-in" role="alertdialog" aria-modal="true" aria-labelledby="safety-title" (click)="safetyService.dismissModal()">
        <div class="safety-dialog animate-scale-up" (click)="$event.stopPropagation()">
          <!-- Clean Minimalist Header -->
          <div class="safety-header">
            <div class="header-left">
              <div class="alert-icon-box">
                <span class="alert-symbol">🚨</span>
              </div>
              <div>
                <div class="badge-row">
                  <span class="soft-alert-badge">BREAK-OUT</span>
                  <span class="badge-sub">জরুরি নিরাপত্তা সতর্কতা</span>
                </div>
                <h2 id="safety-title" class="safety-title">নিরাপত্তা সহায়তা ও দিকনির্দেশনা</h2>
              </div>
            </div>
            <button
              type="button"
              class="close-icon-btn"
              (click)="safetyService.dismissModal()"
              title="পপআপ বন্ধ করুন"
              aria-label="Close"
            >
              ✕
            </button>
          </div>

          <!-- Main Content Body -->
          <div class="safety-body">
            <!-- Primary Urgent Prompt Box -->
            <div class="alert-prompt-box">
              <div class="prompt-icon">⚠️</div>
              <div class="prompt-text">
                <h3 class="prompt-headline">আপনি এখনই বিপদে আছেন বলে মনে হচ্ছে।</h3>
                <p class="prompt-reason">
                  {{
                    safetyService.safetyReason() ||
                    'শারীরিক নির্যাতন, হুমকি বা তাৎক্ষণিক ঝুঁকির ইঙ্গিত পাওয়া গেছে। আপনার সুরক্ষা সবার আগে।'
                  }}
                </p>
                <p class="prompt-instruction">
                  স্বয়ংক্রিয় ফরম পূরণ স্থগিত করা হয়েছে। তাৎক্ষণিক সুরক্ষার জন্য নিচে সরাসরি কল করুন:
                </p>
              </div>
            </div>

            <!-- Subtle Voice Status Bar -->
            <div class="voice-status-bar">
              <div class="voice-status-left">
                <span class="speaker-icon">🔊</span>
                <span>ভয়েসে জরুরি দিকনির্দেশনা শোনানো হচ্ছে</span>
              </div>
              <div class="voice-status-actions">
                <button type="button" class="voice-ctrl-btn" (click)="safetyService.speakEmergencyNotice()">
                  🔁 পুনরায় শুনুন
                </button>
                <button type="button" class="voice-ctrl-btn mute" (click)="safetyService.stopSpeaking()">
                  🔇 নিঃশব্দ
                </button>
              </div>
            </div>

            <!-- Unified Helplines Section -->
            <div class="helplines-container">
              <!-- 1. National Legal Aid Helpline 16430 -->
              <div class="helpline-row">
                <div class="row-info">
                  <span class="service-label">জাতীয় হেল্পলাইন (টোল-ফ্রি)</span>
                  <div class="service-number-group">
                    <span class="service-icon">📞</span>
                    <strong class="service-number">১৬৪৩০</strong>
                  </div>
                  <span class="service-desc">সরকারি জাতীয় আইনি সহায়তা ও পরামর্শ</span>
                </div>
                <a href="tel:16430" class="call-btn btn-primary">
                  <span>📞 এখনই কল করুন</span>
                </a>
              </div>

              <!-- 2. National Emergency Service 999 -->
              <div class="helpline-row">
                <div class="row-info">
                  <span class="service-label">জরুরি পুলিশ ও উদ্ধার</span>
                  <div class="service-number-group">
                    <span class="service-icon">🚨</span>
                    <strong class="service-number text-alert">৯৯৯</strong>
                  </div>
                  <span class="service-desc">তাৎক্ষণিক পুলিশ ও অ্যাম্বুলেন্স সহায়তা</span>
                </div>
                <a href="tel:999" class="call-btn btn-alert">
                  <span>🚨 ৯৯৯-এ কল করুন</span>
                </a>
              </div>

              <!-- 3. Legal Aid Clinic Center Phone -->
              <div class="helpline-row">
                <div class="row-info">
                  <span class="service-label">{{ safetyService.clinicName() }}</span>
                  <div class="service-number-group">
                    <span class="service-icon">⚖️</span>
                    <strong class="service-number text-clinic">{{ safetyService.clinicPhone() }}</strong>
                  </div>
                  <span class="service-desc">নিকটস্থ আইন সহায়তা কেন্দ্র ও প্যানেল আইনজীবী</span>
                </div>
                <a [href]="'tel:' + safetyService.clinicPhone()" class="call-btn btn-outline">
                  <span>📞 কেন্দ্রে কল করুন</span>
                </a>
              </div>

              <!-- 4. Women and Children Repression Prevention Helpline 109 -->
              <div class="helpline-row">
                <div class="row-info">
                  <span class="service-label">নারী ও শিশু নির্যাতন প্রতিরোধ সেল</span>
                  <div class="service-number-group">
                    <span class="service-icon">🛡️</span>
                    <strong class="service-number">১০৯</strong>
                  </div>
                  <span class="service-desc">টোল-ফ্রি জরুরি সহায়তা ও আশ্রয় সেল</span>
                </div>
                <a href="tel:109" class="call-btn btn-secondary">
                  <span>📞 ১০৯ এ কল করুন</span>
                </a>
              </div>
            </div>
          </div>

          <!-- Clean Minimalist Footer -->
          <div class="safety-footer">
            <button
              type="button"
              class="safe-exit-btn"
              (click)="safetyService.quickSafeExit()"
              title="স্ক্রিন থেকে অবিলম্বে বের হয়ে নিরাপদ পেজে যান"
            >
              🚪 নিরাপদ প্রস্থান (Quick Exit)
            </button>
            <button
              type="button"
              class="dismiss-action-btn"
              (click)="safetyService.dismissModal()"
            >
              পপআপ বন্ধ করুন
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    /* -----------------------------------------------------------
       Odhikar Clean Dashboard Alert Theme Palette:
       Primary Dark Green: #005A44
       Background Cream:   #F6F3EC
       Surface White:      #FFFFFF
       Soft Gold / Amber:  #FAF0DB
       Soft Gold Border:   #F3DDAC
       Soft Red Alert:     #FDF0ED
       Soft Red Text:      #E37365
       Mint Sage Green:    #EBF4F0
       Primary Text:       #1A2421
       Secondary Text:     #6C7873
       Card Border / Line: #E2E6E3
    ----------------------------------------------------------- */

    .safety-backdrop {
      position: fixed;
      inset: 0;
      z-index: 999999;
      background: rgba(26, 36, 33, 0.65);
      backdrop-filter: blur(6px);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1.25rem;
    }

    .safety-dialog {
      background: #FFFFFF;
      border-radius: 20px;
      max-width: 580px;
      width: 100%;
      border: 1px solid #E2E6E3;
      border-top: 4px solid #E37365;
      box-shadow: 0 20px 45px -10px rgba(26, 36, 33, 0.2), 0 0 0 1px rgba(226, 230, 227, 0.8);
      overflow: hidden;
      display: flex;
      flex-direction: column;
      max-height: 92vh;
      animation: smoothPopIn 0.25s cubic-bezier(0.16, 1, 0.3, 1);
    }

    @keyframes smoothPopIn {
      0% { opacity: 0; transform: scale(0.96) translateY(10px); }
      100% { opacity: 1; transform: scale(1) translateY(0); }
    }

    /* Header */
    .safety-header {
      background: #FFFFFF;
      border-bottom: 1px solid #E2E6E3;
      padding: 1.15rem 1.5rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
    }

    .header-left {
      display: flex;
      align-items: center;
      gap: 0.85rem;
    }

    .alert-icon-box {
      width: 42px;
      height: 42px;
      border-radius: 12px;
      background: #FDF0ED;
      border: 1px solid #F3DDAC;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .alert-symbol {
      font-size: 1.35rem;
    }

    .badge-row {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 0.15rem;
    }

    .soft-alert-badge {
      background: #FDF0ED;
      color: #E37365;
      border: 1px solid #F3DDAC;
      font-size: 0.68rem;
      font-weight: 800;
      letter-spacing: 0.08em;
      padding: 0.15rem 0.5rem;
      border-radius: 6px;
    }

    .badge-sub {
      color: #6C7873;
      font-size: 0.72rem;
      font-weight: 600;
    }

    .safety-title {
      font-size: 1.15rem;
      font-weight: 800;
      color: #1A2421;
      margin: 0;
      line-height: 1.25;
    }

    .close-icon-btn {
      background: #F6F3EC;
      border: 1px solid #E2E6E3;
      color: #6C7873;
      width: 32px;
      height: 32px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 0.95rem;
      cursor: pointer;
      transition: all 0.15s;
    }
    .close-icon-btn:hover {
      background: #E2E6E3;
      color: #1A2421;
    }

    /* Body */
    .safety-body {
      padding: 1.25rem 1.5rem;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 0.9rem;
      background: #FFFFFF;
    }

    /* Urgent Box */
    .alert-prompt-box {
      background: #FDF0ED;
      border: 1.5px solid #F3DDAC;
      border-radius: 14px;
      padding: 1rem 1.15rem;
      display: flex;
      gap: 0.85rem;
      align-items: flex-start;
    }

    .prompt-icon {
      font-size: 1.5rem;
      line-height: 1.1;
      flex-shrink: 0;
    }

    .prompt-text {
      flex: 1;
    }

    .prompt-headline {
      font-size: 1.15rem;
      font-weight: 800;
      color: #1A2421;
      margin: 0 0 0.25rem 0;
      line-height: 1.3;
    }

    .prompt-reason {
      font-size: 0.84rem;
      color: #E37365;
      font-weight: 600;
      margin: 0 0 0.35rem 0;
      line-height: 1.4;
    }

    .prompt-instruction {
      font-size: 0.78rem;
      color: #6C7873;
      margin: 0;
      line-height: 1.35;
    }

    /* Voice Bar */
    .voice-status-bar {
      background: #EBF4F0;
      border: 1px solid #D5E7DF;
      border-radius: 10px;
      padding: 0.55rem 0.85rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      flex-wrap: wrap;
    }

    .voice-status-left {
      display: flex;
      align-items: center;
      gap: 0.45rem;
      font-size: 0.78rem;
      font-weight: 700;
      color: #005A44;
    }

    .speaker-icon {
      font-size: 0.95rem;
    }

    .voice-status-actions {
      display: flex;
      gap: 0.4rem;
    }

    .voice-ctrl-btn {
      background: #FFFFFF;
      border: 1px solid #D5E7DF;
      color: #005A44;
      font-size: 0.72rem;
      font-weight: 700;
      padding: 0.25rem 0.55rem;
      border-radius: 6px;
      cursor: pointer;
      transition: background 0.15s;
    }
    .voice-ctrl-btn:hover {
      background: #F6F3EC;
    }
    .voice-ctrl-btn.mute {
      color: #6C7873;
      border-color: #E2E6E3;
    }

    /* Helpline List */
    .helplines-container {
      display: flex;
      flex-direction: column;
      gap: 0.65rem;
    }

    .helpline-row {
      background: #FFFFFF;
      border: 1px solid #E2E6E3;
      border-radius: 12px;
      padding: 0.75rem 1rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.85rem;
      transition: border-color 0.15s, box-shadow 0.15s;
    }
    .helpline-row:hover {
      border-color: #005A44;
      box-shadow: 0 2px 8px rgba(26, 36, 33, 0.05);
    }

    .row-info {
      display: flex;
      flex-direction: column;
      gap: 0.1rem;
    }

    .service-label {
      font-size: 0.7rem;
      font-weight: 700;
      color: #6C7873;
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }

    .service-number-group {
      display: flex;
      align-items: center;
      gap: 0.35rem;
    }

    .service-icon {
      font-size: 0.95rem;
    }

    .service-number {
      font-size: 1.25rem;
      font-weight: 800;
      color: #1A2421;
      line-height: 1.2;
    }
    .service-number.text-alert {
      color: #E37365;
    }
    .service-number.text-clinic {
      font-size: 1.05rem;
      color: #005A44;
    }

    .service-desc {
      font-size: 0.72rem;
      color: #6C7873;
    }

    /* Buttons */
    .call-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: 0.55rem 1rem;
      border-radius: 10px;
      font-size: 0.82rem;
      font-weight: 700;
      text-decoration: none;
      white-space: nowrap;
      transition: all 0.15s;
    }

    .btn-primary {
      background: #005A44;
      color: #FFFFFF;
    }
    .btn-primary:hover {
      background: #004533;
    }

    .btn-alert {
      background: #FDF0ED;
      color: #E37365;
      border: 1px solid #E37365;
    }
    .btn-alert:hover {
      background: #E37365;
      color: #FFFFFF;
    }

    .btn-outline {
      background: #FAF0DB;
      color: #1A2421;
      border: 1px solid #F3DDAC;
    }
    .btn-outline:hover {
      background: #F3DDAC;
    }

    .btn-secondary {
      background: #F6F3EC;
      color: #1A2421;
      border: 1px solid #E2E6E3;
    }
    .btn-secondary:hover {
      background: #E2E6E3;
    }

    /* Footer */
    .safety-footer {
      background: #F6F3EC;
      border-top: 1px solid #E2E6E3;
      padding: 0.85rem 1.5rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      flex-wrap: wrap;
    }

    .safe-exit-btn {
      background: #1A2421;
      color: #FFFFFF;
      border: none;
      font-size: 0.78rem;
      font-weight: 700;
      padding: 0.55rem 0.9rem;
      border-radius: 8px;
      cursor: pointer;
      transition: background 0.15s;
    }
    .safe-exit-btn:hover {
      background: #000000;
    }

    .dismiss-action-btn {
      background: transparent;
      border: 1px solid #E2E6E3;
      color: #6C7873;
      font-size: 0.78rem;
      font-weight: 600;
      padding: 0.5rem 0.85rem;
      border-radius: 8px;
      cursor: pointer;
      transition: all 0.15s;
    }
    .dismiss-action-btn:hover {
      background: #FFFFFF;
      color: #1A2421;
    }
  `]
})
export class SafetyBreakoutModalComponent {
  readonly safetyService = inject(SafetyEscalationService);
}

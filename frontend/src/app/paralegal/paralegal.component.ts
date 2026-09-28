import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { SpeechService } from '../core/speech.service';
import { ApplicationTemplateDto } from '../core/intake.models';
import { AuthService } from '../core/auth.service';
import { LegalUserProfile, DEMO_LEGAL_PROFILES } from '../core/auth.models';
import { ParalegalLoginComponent } from '../auth/paralegal-login/paralegal-login.component';
import jsPDF from 'jspdf';
import html2canvas from 'html2canvas';

import {
  CaseField,
  ScheduledSlot,
  QuestionBankItemDto,
  QuestionWiseReply,
  LegalChecklistItem,
  getDefaultLegalChecklist,
  ParalegalCase
} from '../core/paralegal.models';
import { DEFAULT_QUESTION_BANK } from '../shared/constants/question-bank.constants';
import { MONGO_APPLICATION_TEMPLATES } from '../shared/constants/application-template.constants';

export type {
  CaseField,
  ScheduledSlot,
  QuestionBankItemDto,
  QuestionWiseReply,
  LegalChecklistItem,
  ParalegalCase
};
export {
  getDefaultLegalChecklist,
  DEFAULT_QUESTION_BANK
};

@Component({
  selector: 'app-paralegal',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ParalegalLoginComponent],
  templateUrl: './paralegal.component.html',
  styleUrls: ['./paralegal.component.css']
})
export class ParalegalComponent implements OnInit {
  private api = inject(ApiService);
  public speechService = inject(SpeechService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  public authService = inject(AuthService);

  get currentUser(): LegalUserProfile {
    return this.authService.currentUser;
  }

  isLoggedIn(): boolean {
    return this.authService.isLoggedIn();
  }
  isProfileModalOpen = false;
  activeDetailTab: 'summary' | 'checklist' = 'summary';

  openProfileModal(): void {
    this.isProfileModalOpen = true;
  }

  closeProfileModal(): void {
    this.isProfileModalOpen = false;
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/paralegal/login']);
  }

  switchProfile(profileId: string): void {
    this.authService.loginWithProfile(profileId);
    this.toastMessage = 'প্রোফাইল সফলভাবে পরিবর্তন করা হয়েছে।';
    setTimeout(() => this.toastMessage = null, 3000);
  }

  getLegalChecklist(c: ParalegalCase): LegalChecklistItem[] {
    if (!c.legalChecklist) {
      c.legalChecklist = getDefaultLegalChecklist(c.category);
    }
    return c.legalChecklist;
  }

  toggleChecklistItem(c: ParalegalCase, item: LegalChecklistItem): void {
    item.isChecked = !item.isChecked;
  }

  markAllChecklistItems(c: ParalegalCase): void {
    const list = this.getLegalChecklist(c);
    list.forEach(i => i.isChecked = true);
    this.toastMessage = 'আইনি চেকলিস্টের সকল দফা যাচাইকৃত হিসেবে চিহ্নিত করা হয়েছে।';
    setTimeout(() => this.toastMessage = null, 3000);
  }

  getChecklistCompletedCount(c: ParalegalCase): number {
    return this.getLegalChecklist(c).filter(i => i.isChecked).length;
  }

  getChecklistScore(c: ParalegalCase): number {
    const list = this.getLegalChecklist(c);
    if (list.length === 0) return 0;
    const checked = list.filter(i => i.isChecked).length;
    return Math.round((checked / list.length) * 100);
  }


  goToHome(): void {
    this.router.navigate(['/']);
  }

  cases: ParalegalCase[] = [];
  selectedCase: ParalegalCase | null = null;
  applicationTemplates: ApplicationTemplateDto[] = MONGO_APPLICATION_TEMPLATES;
  activeTemplate: ApplicationTemplateDto | null = null;

  isLoadingCases = false;
  isLoadingSummary = false;
  initialCaseId: string | null = null;
  initialConversationId: string | null = null;

  // Dynamic legal document draft state (filled from application_templates)
  renderedRecipient: string = '';
  renderedRecipientHtml: string = '';
  renderedSubject: string = '';
  renderedBodyHtml: string = '';
  renderedDisclaimer: string = '';

  // Editing state
  isEditingSummary = false;
  tempFieldValues: { [key: string]: string } = {};

  // Reassign Slot Modal State
  isReslotModalOpen = false;
  reslotCaseId = '';
  reslotDate = '';
  reslotTime = '11:00';
  reslotCounselor = 'এডভোকেট সুলতানা কামাল (প্যানেল আইনজীবী)';
  reslotNotes = '';

  // Legal Draft Preview Modal State
  isDraftPreviewOpen = false;
  isDownloadingPdf = false;
  toastMessage: string | null = null;

  // Audio simulation state
  isPlayingAudio = false;

  // Question-wise Replies State
  isQuestionRepliesOpen = false;
  currentlyPlayingQrIndex: number | null = null;
  isPlayingAllReplies = false;
  private playAllTimeout: any = null;

  // Question Bank & Case Answers State (MongoDB)
  questionBankByCategory: Record<string, QuestionBankItemDto[]> = {};
  caseAnswersByCaseId: Record<string, Record<string, string>> = {};
  isLoadingQuestionBank = false;
  qrFilterMode: 'all' | 'answered' = 'all';

  todayDateString = new Date().toLocaleDateString('bn-BD');

  // Mobile Responsive View State ('queue' vs 'detail')
  activeMobileView: 'queue' | 'detail' = 'detail';

  activeTab: 'new' | 'urgent' | 'reassign_slot' | 'approved' | 'all' = 'all';

  isCaseApprovedCategory(c: ParalegalCase): boolean {
    return !!(c.isApproved || c.status === 'APPROVED' || c.status === 'RESOLVED' || c.status === 'approved' || c.categoryTab === 'approved');
  }

  isCaseReslotCategory(c: ParalegalCase): boolean {
    if (this.isCaseApprovedCategory(c)) return false;
    return !!(c.isReslot || c.status === 'REASSIGN_SLOT' || c.status === 'ASSIGNED' || c.status === 'reassign_slot' || c.categoryTab === 'reassign_slot' || c.scheduledSlot);
  }

  isCaseUrgentCategory(c: ParalegalCase): boolean {
    if (this.isCaseApprovedCategory(c) || this.isCaseReslotCategory(c)) return false;
    return !!(c.isUrgent || c.status === 'URGENT' || c.status === 'urgent' || c.categoryTab === 'urgent');
  }

  isCaseNewCategory(c: ParalegalCase): boolean {
    return !this.isCaseApprovedCategory(c) && !this.isCaseReslotCategory(c) && !this.isCaseUrgentCategory(c);
  }

  get approvedCount(): number {
    return this.cases.filter(c => this.isCaseApprovedCategory(c)).length;
  }

  get reslotCount(): number {
    return this.cases.filter(c => this.isCaseReslotCategory(c)).length;
  }

  get urgentCount(): number {
    return this.cases.filter(c => this.isCaseUrgentCategory(c)).length;
  }

  get newCount(): number {
    return this.cases.filter(c => this.isCaseNewCategory(c)).length;
  }

  get filteredCases(): ParalegalCase[] {
    if (this.activeTab === 'approved') {
      return this.cases.filter(c => this.isCaseApprovedCategory(c));
    }
    if (this.activeTab === 'reassign_slot') {
      return this.cases.filter(c => this.isCaseReslotCategory(c));
    }
    if (this.activeTab === 'urgent') {
      return this.cases.filter(c => this.isCaseUrgentCategory(c));
    }
    if (this.activeTab === 'new') {
      return this.cases.filter(c => this.isCaseNewCategory(c));
    }
    return this.cases;
  }

  setTab(tab: 'new' | 'urgent' | 'reassign_slot' | 'approved' | 'all'): void {
    this.activeTab = tab;
    const filtered = this.filteredCases;
    if (filtered.length > 0) {
      if (!this.selectedCase || !filtered.some(c => c.id === this.selectedCase?.id)) {
        this.selectCase(filtered[0]);
      }
    }
  }

  get draftComplaintBody(): string {
    if (!this.selectedCase) return '';
    if (this.selectedCase.draftComplaintText && this.selectedCase.draftComplaintText.trim().length > 30) {
      return this.selectedCase.draftComplaintText;
    }
    return this.generateDefaultComplaintText(this.selectedCase);
  }

  ngOnInit(): void {
    // 1. Read query parameters
    this.route.queryParams.subscribe(params => {
      this.initialCaseId = params['caseId'] || null;
      this.initialConversationId = params['conversationId'] || null;
      if (!this.initialCaseId && typeof window !== 'undefined') {
        this.initialCaseId = sessionStorage.getItem('lastSelectedCaseId') || null;
      }
    });

    // 2. Fetch official application templates from MongoDB application_templates collection
    this.loadApplicationTemplates();

    // 3. Fetch cases from MongoDB
    this.loadCasesFromBackend();
  }

  loadApplicationTemplates(): void {
    this.api.getApplicationTemplates().subscribe({
      next: (templates) => {
        if (Array.isArray(templates) && templates.length > 0) {
          this.applicationTemplates = templates;
          if (this.selectedCase) {
            this.renderApplicationTemplateForCase(this.selectedCase);
          }
        }
      },
      error: (err) => {
        console.warn('Failed to load application_templates from DB, using internal collection templates:', err);
      }
    });
  }

  loadCasesFromBackend(): void {
    this.isLoadingCases = true;
    this.api.getParalegalCases().subscribe({
      next: (data: any[]) => {
        this.isLoadingCases = false;
        if (Array.isArray(data) && data.length > 0) {
          const mapped = data.map(bc => this.mapBackendCaseToParalegalCase(bc));
          this.cases = mapped;

          // If query param passed, auto-select it; otherwise select newest
          const targetCaseId = this.initialCaseId || (typeof window !== 'undefined' ? sessionStorage.getItem('lastSelectedCaseId') : null);
          if (targetCaseId) {
            const found = this.cases.find(c => c.id === targetCaseId || c.reference === targetCaseId);
            this.selectedCase = found || this.cases[0];
          } else {
            this.selectedCase = this.cases[0];
          }

          if (this.selectedCase) {
            this.fetchTemplateSummary(this.selectedCase);
            if (this.selectedCase.category) {
              this.loadQuestionBankForCategory(this.selectedCase.category);
            }
            if (this.selectedCase.id) {
              this.loadCaseAnswers(this.selectedCase.id);
            }
          }
        } else {
          this.cases = [];
          this.selectedCase = null;
        }
      },
      error: (err) => {
        this.isLoadingCases = false;
        console.error('Failed to fetch cases from MongoDB:', err);
        this.cases = [];
        this.selectedCase = null;
      }
    });
  }

  selectCase(item: ParalegalCase): void {
    if (this.isEditingSummary) {
      this.cancelEdit();
    }
    this.stopPlayingReplies();
    this.selectedCase = item;
    this.activeMobileView = 'detail';
    if (item?.id && typeof window !== 'undefined') {
      sessionStorage.setItem('lastSelectedCaseId', item.id);
      this.router.navigate([], { queryParams: { caseId: item.id }, queryParamsHandling: 'merge' });
    }
    this.speechService.stopSpeaking();
    this.isPlayingAudio = false;

    // Fetch dynamic template summary and render application template
    this.fetchTemplateSummary(item);

    // Fetch questions from question bank based on incident type / category
    if (item?.category) {
      this.loadQuestionBankForCategory(item.category);
    }
    if (item?.id) {
      this.loadCaseAnswers(item.id);
    }
  }

  /**
   * Fetches the dynamic parent category template summary from MongoDB
   * and renders the matching PDF application template.
   */
  fetchTemplateSummary(item: ParalegalCase): void {
    if (!item || !item.id) return;

    this.isLoadingSummary = true;
    this.api.getCaseSummaryFromTemplate(item.id).subscribe({
      next: (summary: any) => {
        this.isLoadingSummary = false;
        if (summary && Array.isArray(summary.fields) && summary.fields.length > 0) {
          item.templateTitle = summary.parentCategoryName;
          item.completionRate = summary.completionRate;

          item.fields = summary.fields.map((f: any) => {
            const isMissing = f.status === 'Missing' || !f.value || f.value === 'তথ্য পাওয়া যায়নি' || f.value === 'জানা নেই';
            let badge: 'high-confidence' | 'inferred' | 'none-raised' | 'verified' | null = null;
            let badgeText = '';

            if (f.status === 'Stated' && !isMissing) {
              badge = 'high-confidence';
              badgeText = 'high confidence';
            } else if ((f.status === 'Inferred' || f.isInferred) && !isMissing) {
              badge = 'inferred';
              badgeText = 'অনুমানকৃত / Inferred';
            } else if (isMissing) {
              badge = 'none-raised';
              badgeText = 'তথ্য পাওয়া যায়নি';
            }

            return {
              key: f.key,
              label: f.labelBn,
              value: isMissing ? 'তথ্য পাওয়া যায়নি' : f.value,
              badge: badge,
              badgeText: badgeText,
              isInferred: f.isInferred || f.status === 'Inferred'
            };
          });

          // Refresh active view and render application template from MongoDB
          if (this.selectedCase?.id === item.id) {
            this.selectedCase = { ...item };
            this.renderApplicationTemplateForCase(this.selectedCase, summary);
          }
        } else {
          this.renderApplicationTemplateForCase(item);
        }
      },
      error: (err) => {
        this.isLoadingSummary = false;
        console.warn('Template summary fetch failed for case:', item.id, err);
        this.renderApplicationTemplateForCase(item);
      }
    });
  }

  /**
   * Matches and fills the PDF application template from MongoDB `application_templates`
   * based on the parent-incident-category, resolving values from case summary and
   * setting fallbackBn for missing values from the case summary / template specification.
   */
      get applicantDisplayName(): string {
    if (!this.selectedCase) return '[আবেদনকারীর নাম]';
    const found = this.selectedCase.fields?.find(f =>
      f.key.toLowerCase().includes('applicant') ||
      f.key.toLowerCase().includes('worker') ||
      f.key.toLowerCase().includes('mother') ||
      f.key.toLowerCase().includes('victim')
    );
    if (found && found.value && !found.value.includes('তথ্য পাওয়া যায়নি') && !found.value.includes('জানা নেই')) {
      return found.value.trim();
    }
    const applicantVal = (this.selectedCase as any).applicant;
    if (applicantVal && !applicantVal.includes('তথ্য পাওয়া যায়নি') && !applicantVal.includes('জানা নেই')) {
      return applicantVal.trim();
    }
    return '[আবেদনকারীর নাম]';
  }

  /**
   * Matches and fills the PDF application template from MongoDB `application_templates`
   * based on the parent-incident-category, resolving values from case summary.
   */
  renderApplicationTemplateForCase(item: ParalegalCase, summary?: any): void {
    if (!item) return;

    const rawCategory = (summary?.parentCategoryCode || item.category || '').toUpperCase();
    const parentCategoryCode = this.normalizeParentCategoryCode(rawCategory);

    // Find matching template from MongoDB collection
    const template =
      this.applicationTemplates.find(t => t.categoryCode?.toUpperCase() === parentCategoryCode) ||
      this.applicationTemplates.find(t => t.categoryCode?.toUpperCase() === 'OTHER') ||
      MONGO_APPLICATION_TEMPLATES[0];

    this.activeTemplate = template;
    item.templateTitle = template.titleBn;

    // Helper to get stated value from case summary fields
    const getSummaryFieldValue = (candidateKeys: string[]): string | null => {
      for (const k of candidateKeys) {
        const cleanK = k.replace(/_/g, '').toLowerCase();
        const found = item.fields?.find(f => {
          if (!f?.key) return false;
          const fKeyClean = f.key.replace(/_/g, '').toLowerCase();
          return fKeyClean === cleanK || f.key.toLowerCase() === k.toLowerCase();
        });
        if (
          found &&
          found.value &&
          found.value.trim() !== '' &&
          found.value !== 'তথ্য পাওয়া যায়নি' &&
          found.value !== 'জানা নেই' &&
          !found.value.includes('তথ্য পাওয়া যায়নি') &&
          !found.value.includes('জানা নেই')
        ) {
          return found.value.trim();
        }
      }
      return null;
    };

    const getFallback = (placeholderKey: string, defaultFallback = '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]'): string => {
      const pl = template.supportedPlaceholders?.find(p => p.key === placeholderKey || p.placeholder === placeholderKey);
      if (pl && pl.fallbackBn) {
        return pl.fallbackBn;
      }
      return defaultFallback;
    };

    const district = item.district && item.district !== 'তথ্য পাওয়া যায়নি' && item.district !== 'জানা নেই' ? item.district : 'ঢাকা';

    const applicantResolved =
      getSummaryFieldValue(['applicant', 'applicantName', 'motherName', 'workerName', 'victim', 'clientName']) ||
      ((item as any).applicant && (item as any).applicant !== 'তথ্য পাওয়া যায়নি' && (item as any).applicant !== 'জানা নেই' ? (item as any).applicant : null) ||
      getFallback('APPLICANT_NAME', '[আবেদনকারী]');

    const applicantAddressResolved =
      getSummaryFieldValue(['applicantAddress', 'address', 'presentAddress', 'permanentAddress', 'location']) ||
      getFallback('APPLICANT_ADDRESS', `জেলা: ${district}`);

    const respondentResolved =
      getSummaryFieldValue(['respondent', 'respondentName', 'husbandName', 'employerName', 'opposingParty', 'accused']) ||
      ((item as any).respondent && (item as any).respondent !== 'তথ্য পাওয়া যায়নি' && (item as any).respondent !== 'জানা নেই' ? (item as any).respondent : null) ||
      getFallback('RESPONDENT_NAME', '[বিবাদী]');

    const respondentAddressResolved =
      getSummaryFieldValue(['respondentAddress', 'employerAddress', 'opposingAddress']) ||
      getFallback('RESPONDENT_ADDRESS', `জেলা: ${district}`);

    const dowerTotal =
      getSummaryFieldValue(['dowerTotalAmount', 'dowerAmount', 'totalDower', 'dowerTotal', 'claimedAmount']) ||
      getFallback('DOWER_TOTAL_AMOUNT');

    const dowerPaid =
      getSummaryFieldValue(['dowerPaidAmount', 'paidDowerAmount', 'paidDower', 'dowerPaid']) ||
      getFallback('DOWER_PAID_AMOUNT', '০ (শূন্য) টাকা');

    const dowerUnpaid =
      getSummaryFieldValue(['dowerUnpaidAmount', 'unpaidDowerAmount', 'unpaidDower', 'dowerUnpaid', 'dowerAmount']) ||
      getFallback('DOWER_UNPAID_AMOUNT');

    const maintenanceDue =
      getSummaryFieldValue(['maintenanceDue', 'maintenanceClaim', 'maintenanceAmount', 'unpaidMaintenance']) ||
      getFallback('MAINTENANCE_DUE');

    const unpaidWage =
      getSummaryFieldValue(['unpaidWageAmount', 'unpaidWage', 'unpaidAmount', 'claimedAmount']) ||
      getFallback('UNPAID_WAGE_AMOUNT');

    const incidentDetails =
      getSummaryFieldValue(['incidentDetails', 'facts', 'clientStatement']) ||
      item.rawTranscript ||
      item.narrative ||
      getFallback('INCIDENT_DETAILS');

    const placeholderValues: Record<string, string> = {
      '{{APPLICANT_NAME}}': applicantResolved,
      '{{APPLICANT_ADDRESS}}': applicantAddressResolved,
      '{{RESPONDENT_NAME}}': respondentResolved,
      '{{RESPONDENT_ADDRESS}}': respondentAddressResolved,
      '{{DISTRICT}}': district,
      '{{APPLICATION_DATE}}': this.todayDateString,
      '{{CURRENT_DATE}}': this.todayDateString,

      // Dower & Maintenance
      '{{MARRIAGE_DATE}}':
        getSummaryFieldValue(['marriageDate', 'incidentDate', 'date']) ||
        getFallback('MARRIAGE_DATE'),
      '{{DOWER_TOTAL_AMOUNT}}': dowerTotal,
      '{{DOWER_AMOUNT}}': dowerTotal,
      '{{DOWER_PAID_AMOUNT}}': dowerPaid,
      '{{DOWER_UNPAID_AMOUNT}}': dowerUnpaid,
      '{{UNPAID_DOWER_AMOUNT}}': dowerUnpaid,
      '{{MAINTENANCE_DUE}}': maintenanceDue,
      '{{MAINTENANCE_CLAIM}}': maintenanceDue,

      // Dowry & Violence
      '{{DOWRY_DEMAND_AMOUNT}}':
        getSummaryFieldValue(['dowryDemandAmount', 'dowryAmount', 'claimedAmount']) ||
        getFallback('DOWRY_DEMAND_AMOUNT'),
      '{{ABUSE_TYPE}}':
        getSummaryFieldValue(['abuseType', 'tortureType', 'safetyRisk', 'safetyReason']) ||
        getFallback('ABUSE_TYPE'),
      '{{INJURY_DETAILS}}':
        getSummaryFieldValue(['injuryDetails', 'assaultDetails', 'medicalDetails']) ||
        getFallback('INJURY_DETAILS'),
      '{{INCIDENT_LOCATION}}':
        getSummaryFieldValue(['incidentLocation', 'location', 'address']) ||
        district,
      '{{IMMEDIATE_THREAT}}':
        getSummaryFieldValue(['immediateThreat', 'safetyRisk', 'threatDetails', 'safetyReason']) ||
        getFallback('IMMEDIATE_THREAT'),

      // Labour & Wage
      '{{JOB_DESIGNATION}}':
        getSummaryFieldValue(['jobDesignation', 'designation', 'position', 'occupation']) ||
        getFallback('JOB_DESIGNATION'),
      '{{EMPLOYER_NAME}}':
        getSummaryFieldValue(['employerName', 'companyName', 'factoryName', 'respondent']) ||
        respondentResolved,
      '{{MONTHLY_SALARY}}':
        getSummaryFieldValue(['monthlySalary', 'salary', 'wage']) ||
        getFallback('MONTHLY_SALARY'),
      '{{UNPAID_MONTHS}}':
        getSummaryFieldValue(['unpaidMonths', 'dueMonths', 'monthsCount']) ||
        getFallback('UNPAID_MONTHS'),
      '{{UNPAID_WAGE_AMOUNT}}': unpaidWage,
      '{{UNPAID_WAGE}}': unpaidWage,
      '{{OVERTIME_DUE}}':
        getSummaryFieldValue(['overtimeDue', 'overtimeAmount', 'overtime']) ||
        getFallback('OVERTIME_DUE', '০ (শূন্য) টাকা'),

      // Land & Property
      '{{PROPERTY_DESCRIPTION}}':
        getSummaryFieldValue(['propertyDescription', 'landDescription', 'schedule', 'landDetails']) ||
        getFallback('PROPERTY_DESCRIPTION'),
      '{{OWNERSHIP_TYPE}}':
        getSummaryFieldValue(['ownershipType', 'titleType']) ||
        getFallback('OWNERSHIP_TYPE', 'পৈত্রিক / ক্রয়সূত্রে স্বত্বাধিকারী'),
      '{{DISPUTE_NATURE}}':
        getSummaryFieldValue(['disputeNature', 'disputeType', 'conflictType']) ||
        getFallback('DISPUTE_NATURE'),
      '{{FINANCIAL_LOSS}}':
        getSummaryFieldValue(['financialLoss', 'lossAmount', 'damageAmount']) ||
        getFallback('FINANCIAL_LOSS'),

      // Child & Women Protection
      '{{CHILD_DETAILS}}':
        getSummaryFieldValue(['childDetails', 'childrenInfo', 'childCount']) ||
        getFallback('CHILD_DETAILS'),
      '{{DEPRIVATION_NATURE}}':
        getSummaryFieldValue(['deprivationNature', 'movementRestriction', 'custodyDisputeNature', 'economicControl']) ||
        getFallback('DEPRIVATION_NATURE'),
      '{{CHILD_SAFETY_RISK}}':
        getSummaryFieldValue(['childSafetyRisk', 'safetyRisk', 'immediateThreat']) ||
        getFallback('CHILD_SAFETY_RISK'),
      '{{CUSTODY_DISPUTE}}':
        getSummaryFieldValue(['custodyDisputeNature', 'custodyDispute', 'desiredRelief']) ||
        getFallback('CUSTODY_DISPUTE'),
      '{{DESIRED_RELIEF}}':
        getSummaryFieldValue(['desiredRelief', 'prayer', 'relief']) ||
        getFallback('DESIRED_RELIEF'),

      // Narrative & Evidence
      '{{INCIDENT_DETAILS}}': incidentDetails,
      '{{EVIDENCE_LIST}}':
        getSummaryFieldValue(['evidenceList', 'evidence', 'documents']) ||
        getFallback('EVIDENCE_LIST')
    };

    let body = template.bodyTemplateBn || this.generateDefaultComplaintText(item);
    let disclaimer = template.disclaimerNoticeBn || 'সতর্কীকরণ ও আইনি বিজ্ঞপ্তি (DRAFT - PENDING REVIEW): এই আবেদনপত্রটি অধিকার (Odhikar) ডিজিটাল সহায়িকা কর্তৃক প্রস্তুতকৃত একটি খসড়া রূপরেখা মাত্র। এতে বর্ণিত অভিযোগ ও তথ্যাদি কোনো আদালত বা তদন্তকারী কর্তৃপক্ষ কর্তৃক চূড়ান্তভাবে যাচাই বা প্রমাণিত নয়। বিজ্ঞ আইনজীবী বা সংশ্লিষ্ট আইনি কর্মকর্তার দ্বারা তথ্যসমূহ যাচাই ও স্বাক্ষর ব্যতীত এটি চূড়ান্ত আইনগত নথি হিসেবে গণ্য হবে না।';

    // Apply placeholder substitutions
    for (const token of Object.keys(placeholderValues)) {
      const val = placeholderValues[token];
      body = body.split(token).join(val);
    }

    // Replace any remaining unpopulated placeholders
    body = body.replace(/\{\{[A-Z0-9_]+\}\}/g, '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]');

    this.renderedDisclaimer = disclaimer;
    this.renderedBodyHtml = this.formatBodyToHtml(body);
    item.draftComplaintText = body;
  }

  normalizeParentCategoryCode(code: string): string {
    const c = (code || '').toUpperCase();
    if (c.includes('DOWER') || c.includes('MAINTENANCE')) return 'DOWER_MAINTENANCE';
    if (c.includes('DOWRY') || c.includes('DOMESTIC') || c.includes('PHYSICAL') || c.includes('ASSAULT')) return 'DOWRY_DOMESTIC_VIOLENCE';
    if (c.includes('LABOUR') || c.includes('WAGE') || c.includes('SALARY')) return 'LABOUR_WAGE';
    if (c.includes('LAND') || c.includes('PROPERTY') || c.includes('DISPUTE')) return 'LAND_PROPERTY';
    if (c.includes('WOMEN') || c.includes('CHILD') || c.includes('CUSTODY')) return 'WOMEN_CHILD_PROTECTION';
    return 'OTHER';
  }

  formatBodyToHtml(text: string): string {
    if (!text) return '';
    const paragraphs = text.split(/\n\s*\n/);
    return paragraphs
      .map(p => {
        const trimmed = p.trim();
        if (!trimmed) return '';
        const withBreaks = trimmed.replace(/\n/g, '<br/>');
        return `<p class="mb-3">${withBreaks}</p>`;
      })
      .join('');
  }

  // --- Edit Summary Workflow ---
  toggleEditSummary(): void {
    if (!this.selectedCase) return;

    if (!this.isEditingSummary) {
      this.isEditingSummary = true;
      this.tempFieldValues = {};
      for (const f of this.selectedCase.fields) {
        this.tempFieldValues[f.key] = f.value;
      }
    } else {
      this.saveSummaryEdits();
    }
  }

  saveSummaryEdits(): void {
    if (!this.selectedCase) return;

    const fieldsPayload: Record<string, string> = {};

    for (const f of this.selectedCase.fields) {
      if (this.tempFieldValues[f.key] !== undefined) {
        f.value = this.tempFieldValues[f.key];
        fieldsPayload[f.key] = f.value;
        const isMissing = !f.value || f.value === 'তথ্য পাওয়া যায়নি' || f.value === 'জানা নেই';
        if (!isMissing) {
          f.badge = 'verified';
          f.badgeText = '✓ যাচাইকৃত / Verified';
          f.isInferred = false;
        } else {
          f.badge = 'none-raised';
          f.badgeText = 'তথ্য পাওয়া যায়নি';
        }
      }
    }

    if (fieldsPayload['district'] && fieldsPayload['district'] !== 'তথ্য পাওয়া যায়নি') {
      this.selectedCase.district = fieldsPayload['district'];
    }
    const idx = this.cases.findIndex(c => c.id === this.selectedCase!.id);
    if (idx !== -1) {
      this.cases[idx] = { ...this.selectedCase };
    }

    // Re-render template from MongoDB with updated edited facts
    this.renderApplicationTemplateForCase(this.selectedCase);

    // Persist to backend
    if (this.selectedCase.id) {
      if (typeof window !== 'undefined') {
        sessionStorage.setItem('lastSelectedCaseId', this.selectedCase.id);
      }
      this.api.updateCaseSummary(this.selectedCase.id, {
        fields: fieldsPayload,
        draftText: this.selectedCase.draftComplaintText || ''
      }).subscribe({
        next: (res) => {
          console.log('Case summary & draft saved to backend MongoDB:', res);
          this.showToast('✓ সারসংক্ষেপের তথ্য সফলভাবে ব্যাকএন্ড ডাটাবেজে সংরক্ষিত হয়েছে।');
          if (res?.summary?.completionRate !== undefined) {
            this.selectedCase!.completionRate = res.summary.completionRate;
          }
        },
        error: (err) => {
          console.warn('Backend updateCaseSummary fallback:', err);
          this.api.updateDraft(this.selectedCase!.id, this.selectedCase!.draftComplaintText || '').subscribe({
            next: () => this.showToast('✓ সারসংক্ষেপ ও খসড়া টেক্সট সংরক্ষিত হয়েছে।'),
            error: () => this.showToast('✓ সারসংক্ষেপের তথ্য সংরক্ষিত হয়েছে।')
          });
        }
      });
    } else {
      this.showToast('✓ সারসংক্ষেপের তথ্য সংরক্ষিত হয়েছে।');
    }

    this.isEditingSummary = false;
  }

  cancelEdit(): void {
    this.isEditingSummary = false;
    this.tempFieldValues = {};
  }

  // --- Reassign Slot Modal Workflow ---
  openReslotModal(): void {
    if (!this.selectedCase) return;

    this.reslotCaseId = this.selectedCase.reference;

    const d = new Date();
    d.setDate(d.getDate() + 3);
    this.reslotDate = d.toISOString().split('T')[0];
    this.reslotTime = '11:00';
    this.reslotCounselor = 'এডভোকেট সুলতানা কামাল (প্যানেল আইনজীবী)';
    this.reslotNotes = 'জরুরি আইনি পরামর্শ ও শুনানি।';
    this.isReslotModalOpen = true;
  }

  closeReslotModal(): void {
    this.isReslotModalOpen = false;
  }

  bookSlot(): void {
    if (!this.selectedCase || !this.reslotDate) {
      alert('অনুগ্রহ করে তারিখ নির্বাচন করুন।');
      return;
    }

    this.selectedCase.scheduledSlot = {
      date: this.reslotDate,
      time: this.reslotTime,
      counselor: this.reslotCounselor,
      notes: this.reslotNotes
    };
    this.selectedCase.isReslot = true;
    this.selectedCase.status = 'REASSIGN_SLOT';
    this.selectedCase.statusDisplay = 'স্লট পুনর্নির্ধারণ';
    this.selectedCase.categoryTab = 'reassign_slot';
    this.selectedCase.categoryTabBn = 'স্লট পুনর্নির্ধারণ';

    const slotIdx = this.cases.findIndex(c => c.id === this.selectedCase!.id);
    if (slotIdx !== -1) {
      this.cases[slotIdx] = { ...this.selectedCase };
    }

    // Persist reslot appointment to real MongoDB backend
    if (this.selectedCase.id) {
      const [hours, minutes] = (this.reslotTime || '10:00').split(':');
      const slotDateTime = new Date(this.reslotDate);
      if (!isNaN(slotDateTime.getTime())) {
        slotDateTime.setHours(parseInt(hours || '10', 10), parseInt(minutes || '0', 10), 0);
      }

      this.api.reassignSlot(this.selectedCase.id, {
        clinicId: this.selectedCase.district || 'ঢাকা আইনি সহায়তা কেন্দ্র',
        scheduledSlot: !isNaN(slotDateTime.getTime()) ? slotDateTime.toISOString() : new Date().toISOString()
      }).subscribe({
        next: (res) => {
          console.log('Slot reassigned in MongoDB:', res);
        },
        error: (err) => {
          console.warn('reassignSlot fallback to updateCaseStatus:', err);
          this.api.updateCaseStatus(this.selectedCase!.id!, 'REASSIGN_SLOT').subscribe();
        }
      });
    }

    this.isReslotModalOpen = false;
    this.showToast(`✓ কেস [${this.selectedCase.reference}] এর জন্য সময়সূচী বুক করা হয়েছে এবং স্লট পুনর্নির্ধারণ ট্যাবে স্থানান্তরিত হয়েছে: ${this.formatBengaliDate(this.reslotDate)} (${this.reslotTime})`);
  }

  // --- Approve & Export PDF Workflow (Direct Download without popup) ---
    isCaseApproved(item: ParalegalCase | null = this.selectedCase): boolean {
    if (!item) return false;
    const s = (item.status || '').toUpperCase();
    return item.isApproved === true || s === 'APPROVED' || s === 'RESOLVED' || s === 'অনুমোদিত';
  }

  approveCase(): void {
    if (!this.selectedCase) return;

    const caseId = this.selectedCase.id;
    this.selectedCase.isApproved = true;
    this.selectedCase.status = 'APPROVED';
    this.selectedCase.statusDisplay = 'অনুমোদিত';
    this.selectedCase.categoryTab = 'approved';
    this.selectedCase.categoryTabBn = 'অনুমোদিত';

    const match = this.cases.find(c => c.id === caseId);
    if (match) {
      match.isApproved = true;
      match.status = 'APPROVED';
      match.statusDisplay = 'অনুমোদিত';
      match.categoryTab = 'approved';
      match.categoryTabBn = 'অনুমোদিত';
    }

    if (caseId) {
      this.api.updateCaseStatus(caseId, 'APPROVED').subscribe({
        next: () => {
          this.showToast(`✓ কেস [${this.selectedCase?.reference}] সফলভাবে অনুমোদিত হয়েছে এবং অনুমোদিত ট্যাবে স্থানান্তরিত হয়েছে! এখন PDF ডাউনলোড সক্রিয়।`);
        },
        error: (err) => {
          console.warn('Status update backend notification:', err);
          this.showToast(`✓ কেস [${this.selectedCase?.reference}] অনুমোদিত হয়েছে।`);
        }
      });
    } else {
      this.showToast(`✓ কেস [${this.selectedCase.reference}] সফলভাবে অনুমোদিত হয়েছে এবং অনুমোদিত ট্যাবে স্থানান্তরিত হয়েছে!`);
    }
  }

  async approveAndExportPdf(): Promise<void> {
    this.approveCase();
  }

  async downloadPdfDocument(): Promise<void> {
    if (!this.selectedCase) return;

    if (!this.isCaseApproved()) {
      this.showToast('⚠️ পিডিএফ ডাউনলোড করার পূর্বে কেসটি অনুমোদন (Approve) করুন।');
      return;
    }

    this.isDownloadingPdf = true;

    setTimeout(async () => {
      try {
        const renderElement =
          document.getElementById('legal-complaint-draft-source') ||
          document.getElementById('legal-complaint-draft-printable');

        if (renderElement) {
          const canvas = await html2canvas(renderElement, {
            scale: 2,
            useCORS: true,
            backgroundColor: '#ffffff',
            logging: false
          });

          const imgData = canvas.toDataURL('image/jpeg', 0.95);
          const pdf = new jsPDF({
            orientation: 'portrait',
            unit: 'mm',
            format: 'a4'
          });

          const pageWidth = 210;
          const pageHeight = 297;
          const margin = 10;
          const contentWidth = pageWidth - (margin * 2);
          const contentHeight = (canvas.height * contentWidth) / canvas.width;

          pdf.addImage(imgData, 'JPEG', margin, margin, contentWidth, Math.min(contentHeight, pageHeight - (margin * 2)));

          const safeRef = (this.selectedCase?.reference || 'ODH-2026-0001').replace(/[^a-zA-Z0-9_-]/g, '_');
          const fileName = `${safeRef}-Complaint-Draft.pdf`;

          const pdfBlob = pdf.output('blob');
          const blobUrl = URL.createObjectURL(pdfBlob);
          const link = document.createElement('a');
          link.style.display = 'none';
          link.href = blobUrl;
          link.setAttribute('download', fileName);
          document.body.appendChild(link);
          link.click();

          setTimeout(() => {
            if (document.body.contains(link)) {
              document.body.removeChild(link);
            }
            URL.revokeObjectURL(blobUrl);
          }, 2000);

          this.showToast(`✓ পিডিএফ ফাইল [${fileName}] সফলভাবে ডাউনলোড হয়েছে!`);
        } else {
          this.printDraftDocument();
        }
      } catch (err) {
        console.error('PDF generation error:', err);
        this.showToast('⚠️ সরাসরি প্রিন্ট অপশন খোলা হচ্ছে...');
        this.printDraftDocument();
      } finally {
        this.isDownloadingPdf = false;
      }
    }, 250);
  }

  downloadOfficialBackendPdf(): void {
    if (!this.selectedCase) return;

    if (!this.isCaseApproved()) {
      this.showToast('⚠️ পিডিএফ ডাউনলোড করার পূর্বে কেসটি অনুমোদন (Approve) করুন।');
      return;
    }

    if (this.selectedCase.id) {
      const url = this.api.exportPdfUrl(this.selectedCase.id);
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', `${this.selectedCase.reference}.pdf`);
      link.setAttribute('target', '_blank');
      document.body.appendChild(link);
      link.click();
      setTimeout(() => {
        if (document.body.contains(link)) document.body.removeChild(link);
      }, 1000);
      this.showToast(`✓ ব্যাকএন্ড অফিসিয়াল পিডিএফ প্রস্তুত করা হচ্ছে...`);
    } else {
      this.downloadPdfDocument();
    }
  }

  closeDraftPreview(): void {
    this.isDraftPreviewOpen = false;
  }

  printDraftDocument(): void {
    window.print();
  }

  // --- Audio Simulation ---
  toggleAudioPlayback(): void {
    if (!this.selectedCase) return;

    if (this.isPlayingAudio) {
      this.speechService.stopSpeaking();
      this.isPlayingAudio = false;
    } else {
      this.isPlayingAudio = true;
      const textToSpeak = this.selectedCase.rawTranscript || this.selectedCase.narrative;
      this.speechService.speak(textToSpeak, 0.9, () => {
        this.isPlayingAudio = false;
      });
    }
  }

  // --- Question-Wise Replies & Audio Playback (From Question Bank Based on Incident Type) ---
  normalizeCategoryCode(cat: string | null | undefined): string {
    const c = (cat || '').toUpperCase();
    if (c.includes('DOWER') || c.includes('MAINTENANCE')) return 'DOWER_MAINTENANCE';
    if (c.includes('DOWRY') || c.includes('VIOLENCE') || c.includes('ASSAULT')) return 'DOWRY_DOMESTIC_VIOLENCE';
    if (c.includes('LABOUR') || c.includes('WAGE') || c.includes('SALARY')) return 'LABOUR_WAGE';
    if (c.includes('LAND') || c.includes('PROPERTY') || c.includes('DISPUTE')) return 'LAND_PROPERTY';
    if (c.includes('WOMEN') || c.includes('CHILD') || c.includes('PROTECTION')) return 'WOMEN_CHILD_PROTECTION';
    return 'OTHER';
  }

  loadQuestionBankForCategory(categoryCode: string): void {
    const code = this.normalizeCategoryCode(categoryCode);
    if (this.questionBankByCategory[code] && this.questionBankByCategory[code].length > 0) {
      return;
    }

    this.isLoadingQuestionBank = true;
    this.api.getQuestionBankByParent(code).subscribe({
      next: (questions) => {
        this.isLoadingQuestionBank = false;
        if (Array.isArray(questions) && questions.length > 0) {
          this.questionBankByCategory[code] = questions;
        }
      },
      error: (err) => {
        this.isLoadingQuestionBank = false;
        console.warn(`Failed to fetch question bank from API for ${code}:`, err);
      }
    });
  }

  loadCaseAnswers(caseId: string): void {
    if (!caseId || this.caseAnswersByCaseId[caseId]) return;
    this.api.getCaseAnswers(caseId).subscribe({
      next: (res) => {
        if (res && res.dictionary) {
          this.caseAnswersByCaseId[caseId] = res.dictionary;
        }
      },
      error: (err) => {
        console.warn(`Could not load case answers for ${caseId}:`, err);
      }
    });
  }

  toggleQuestionReplies(): void {
    this.isQuestionRepliesOpen = !this.isQuestionRepliesOpen;
    if (!this.isQuestionRepliesOpen) {
      this.stopPlayingReplies();
    } else if (this.selectedCase?.category) {
      this.loadQuestionBankForCategory(this.selectedCase.category);
      if (this.selectedCase?.id) {
        this.loadCaseAnswers(this.selectedCase.id);
      }
    }
  }

  setQrFilter(mode: 'all' | 'answered'): void {
    this.qrFilterMode = mode;
  }

  getFilteredQuestionReplies(item: ParalegalCase | null = this.selectedCase): QuestionWiseReply[] {
    const all = this.getQuestionReplies(item);
    if (this.qrFilterMode === 'answered') {
      return all.filter(q => q.isAnswered);
    }
    return all;
  }

  getAnsweredCount(item: ParalegalCase | null = this.selectedCase): number {
    return this.getQuestionReplies(item).filter(q => q.isAnswered).length;
  }

  getUnansweredCount(item: ParalegalCase | null = this.selectedCase): number {
    return this.getQuestionReplies(item).filter(q => !q.isAnswered).length;
  }

  getQuestionReplies(item: ParalegalCase | null = this.selectedCase): QuestionWiseReply[] {
    if (!item) return [];
    const replies: QuestionWiseReply[] = [];

    const catCode = this.normalizeCategoryCode(item.category);
    // Fetch questions from Question Bank based on incident type / category
    const qBankItems = (this.questionBankByCategory[catCode] && this.questionBankByCategory[catCode].length > 0)
      ? this.questionBankByCategory[catCode]
      : (DEFAULT_QUESTION_BANK[catCode] || DEFAULT_QUESTION_BANK['OTHER'] || []);

    const answersDict = this.caseAnswersByCaseId[item.id] || {};

    let stepNum = 1;
    for (const q of qBankItems) {
      let answer: string | null = null;
      let isVoice = false;
      const factKey = (q.requiredFactKey || '').toLowerCase();
      const qCat = (q.category || '').toLowerCase();
      const qId = (q.questionId || '').toUpperCase();
      const isThreatQuestion = factKey.includes('threat') || factKey.includes('safety') || qCat.includes('safety') || qCat.includes('threat');

      // Check 1: Answers dictionary from conversation-answers API
      if (factKey && answersDict[factKey]) answer = answersDict[factKey];
      else if (factKey && answersDict[factKey.toUpperCase()]) answer = answersDict[factKey.toUpperCase()];
      else if (qCat && answersDict[qCat]) answer = answersDict[qCat];
      else if (qId && answersDict[qId]) answer = answersDict[qId];

      // Check 2: Extracted fields from MongoDB case summary
      if (!answer && item.fields && Array.isArray(item.fields)) {
        const found = item.fields.find(f => {
          const fk = (f.key || '').toLowerCase();
          if (factKey && fk === factKey) return true;
          if (factKey === 'applicant' && (fk.includes('applicant') || fk.includes('victim') || fk.includes('worker') || fk.includes('mother'))) return true;
          if (factKey === 'respondent' && (fk.includes('respondent') || fk.includes('husband') || fk.includes('employer') || fk.includes('accused'))) return true;
          if ((factKey.includes('dower') || factKey.includes('amount')) && fk.includes('dower')) {
            if (factKey.includes('paid') && fk.includes('paid') && !fk.includes('unpaid')) return true;
            if (factKey.includes('unpaid') && fk.includes('unpaid')) return true;
            if (!factKey.includes('paid') && !factKey.includes('unpaid') && (fk.includes('total') || fk.includes('amount') || fk === 'doweramount' || fk === 'dowertotal')) return true;
          }
          if (factKey.includes('maintenance') && fk.includes('maintenance')) return true;
          if (factKey.includes('marriage') && (fk.includes('marriage') || fk.includes('date'))) return true;
          if (factKey.includes('salary') && (fk.includes('salary') || fk.includes('wage'))) return true;
          if (factKey.includes('child') && fk.includes('child')) return true;
          if (factKey.includes('evidence') && fk.includes('evidence')) return true;
          if (factKey.includes('relief') && fk.includes('relief')) return true;
          if (factKey.includes('threat') && (fk.includes('threat') || fk.includes('safety') || fk.includes('alert'))) return true;
          if (factKey.includes('incident') && !isThreatQuestion && (fk.includes('incident') || fk.includes('facts') || fk.includes('details'))) return true;
          return false;
        });

        if (found && found.value && found.value.trim() !== '' && found.value !== 'তথ্য পাওয়া যায়নি' && found.value !== 'জানা নেই' && !found.value.includes('অপ্রতিষ্ঠিত')) {
          const val = found.value.trim();
          const isThreatVal = val.includes('মেরে') || val.includes('হুমকি') || val.includes('খুন') || val.includes('আঘাত');
          // If the extracted value is purely a threat statement, only associate with threat/safety questions
          if (!isThreatVal || isThreatQuestion) {
            answer = val;
            isVoice = found.badge === 'high-confidence';
          }
        }
      }

      // Check 3: Check dialogue turns from intake chatbot
      if (!answer && item.dialogueHistory && Array.isArray(item.dialogueHistory)) {
        for (const turn of item.dialogueHistory) {
          const qText = turn.question || turn.assistantMessage || turn.prompt || '';
          const aText = (turn.answer || turn.transcript || turn.userTranscript || turn.response || turn.text || '').trim();
          if (aText && qText && (qText.includes(q.questionBangla.slice(0, 14)) || q.questionBangla.includes(qText.slice(0, 14)))) {
            const isThreatA = aText.includes('মেরে') || aText.includes('হুমকি') || aText.includes('খুন') || aText.includes('আঘাত');
            if (!isThreatA || isThreatQuestion) {
              answer = aText;
              isVoice = !!turn.isVoice || !!turn.audioUrl;
              break;
            }
          }
        }
      }

      // Check 4: Initial voice narrative matching core threat/incident questions
      if (!answer) {
        const clientTranscript = (item.rawTranscript || item.narrative || '').trim();
        const isThreatStatement = clientTranscript.includes('মেরে') || clientTranscript.includes('হুমকি') || clientTranscript.includes('খুন') || clientTranscript.includes('আঘাত');

        if (clientTranscript && clientTranscript !== 'তথ্য পাওয়া যায়নি') {
          if (isThreatQuestion && (isThreatStatement || item.isUrgent || item.isSafetyEscalated)) {
            answer = clientTranscript;
            isVoice = true;
          } else if (!isThreatStatement && (factKey === 'incidentdescription' || factKey === 'incidentdetails')) {
            answer = clientTranscript;
            isVoice = true;
          }
        }
      }

      const isAnswered = !!(answer && answer.trim() !== '' && answer !== 'তথ্য পাওয়া যায়নি' && answer !== 'জানা নেই' && !answer.includes('অপ্রতিষ্ঠিত'));

      replies.push({
        questionNumber: stepNum++,
        questionId: q.questionId,
        category: q.category,
        requiredFactKey: q.requiredFactKey,
        question: q.questionBangla,
        reply: isAnswered ? answer! : 'তথ্য এখনও প্রদান করা হয়নি',
        isAnswered: isAnswered,
        isVoice: isVoice
      });
    }

    return replies;
  }

  togglePlayQr(qr: QuestionWiseReply): void {
    if (this.currentlyPlayingQrIndex === qr.questionNumber) {
      this.stopPlayingReplies();
    } else {
      this.stopPlayingReplies();
      this.currentlyPlayingQrIndex = qr.questionNumber;
      let textToSpeak: string;
      if (qr.isAnswered) {
        textToSpeak = `প্রশ্ন ${this.formatBengaliNumber(qr.questionNumber)}: ${qr.question}। ক্লায়েন্টের উত্তর: ${qr.reply}।`;
      } else {
        textToSpeak = `প্রশ্ন ${this.formatBengaliNumber(qr.questionNumber)}: ${qr.question}। এই প্রশ্নের উত্তর এখনও পাওয়া যায়নি।`;
      }
      this.speechService.speak(textToSpeak, 0.9, () => {
        if (this.currentlyPlayingQrIndex === qr.questionNumber) {
          this.currentlyPlayingQrIndex = null;
        }
      });
    }
  }

  togglePlayAllReplies(): void {
    if (this.isPlayingAllReplies) {
      this.stopPlayingReplies();
    } else {
      const list = this.getFilteredQuestionReplies(this.selectedCase);
      if (!list || list.length === 0) return;
      this.stopPlayingReplies();
      this.isPlayingAllReplies = true;
      let idx = 0;

      const playNext = () => {
        if (!this.isPlayingAllReplies || idx >= list.length) {
          this.isPlayingAllReplies = false;
          this.currentlyPlayingQrIndex = null;
          return;
        }

        const item = list[idx];
        this.currentlyPlayingQrIndex = item.questionNumber;
        let textToSpeak: string;
        if (item.isAnswered) {
          textToSpeak = `প্রশ্ন ${this.formatBengaliNumber(item.questionNumber)}: ${item.question}। ক্লায়েন্টের উত্তর: ${item.reply}।`;
        } else {
          textToSpeak = `প্রশ্ন ${this.formatBengaliNumber(item.questionNumber)}: ${item.question}। এই প্রশ্নের উত্তর এখনও পাওয়া যায়নি।`;
        }
        idx++;

        this.speechService.speak(textToSpeak, 0.9, () => {
          if (!this.isPlayingAllReplies) return;
          this.playAllTimeout = setTimeout(playNext, 650);
        });
      };

      playNext();
    }
  }

  stopPlayingReplies(): void {
    this.isPlayingAllReplies = false;
    this.currentlyPlayingQrIndex = null;
    if (this.playAllTimeout) {
      clearTimeout(this.playAllTimeout);
      this.playAllTimeout = null;
    }
    this.speechService.stopSpeaking();
  }

  formatBengaliNumber(n: number | string): string {
    const bnDigits = ['০', '১', '২', '৩', '৪', '৫', '৬', '৭', '৮', '৯'];
    return String(n).replace(/\d/g, d => bnDigits[parseInt(d, 10)]);
  }

  showToast(msg: string): void {
    this.toastMessage = msg;
    setTimeout(() => {
      if (this.toastMessage === msg) {
        this.toastMessage = null;
      }
    }, 4000);
  }

  formatBengaliDate(isoDate: string): string {
    if (!isoDate) return '';
    const parts = isoDate.split('-');
    if (parts.length === 3) {
      return `${parts[2]}/${parts[1]}/${parts[0]}`;
    }
    return isoDate;
  }

  // --- Backend Case Model Mapper ---
  mapBackendCaseToParalegalCase(bc: any): ParalegalCase {
    const isApproved = bc.status === 'APPROVED' || bc.status === 'RESOLVED' || bc.status === 'approved' || bc.isApproved === true || bc.categoryTab === 'approved';
    const hasSlot = !!(bc.clinicReferral && bc.clinicReferral.scheduledSlot) || !!bc.scheduledSlotBn;
    const isReslot = !isApproved && (hasSlot || bc.status === 'REASSIGN_SLOT' || bc.status === 'ASSIGNED' || bc.status === 'reassign_slot' || bc.isReslot === true || bc.categoryTab === 'reassign_slot');
    const isUrgent = !isApproved && !isReslot && (!!bc.isSafetyEscalated || bc.status === 'URGENT' || bc.status === 'urgent' || bc.categoryTab === 'urgent');

    const tab = isApproved ? 'approved' : (isReslot ? 'reassign_slot' : (isUrgent ? 'urgent' : 'new'));
    const tabBn = isApproved ? 'অনুমোদিত' : (isReslot ? 'স্লট পুনর্নির্ধারণ' : (isUrgent ? 'জরুরি' : 'নতুন'));
    const statusDisplay = isApproved ? 'অনুমোদিত' : (isReslot ? 'স্লট পুনর্নির্ধারণ' : (isUrgent ? 'জরুরি' : 'নতুন'));
    const categoryBn = this.mapCategoryToBn(bc.category);

    const ed = bc.extractedData || {};
    const fields: CaseField[] = [];

    // Build raw transcript from dialogue history
    let rawTranscript = bc.latestTranscript || '';
    if (bc.dialogueHistory && bc.dialogueHistory.length > 0) {
      const userLines = bc.dialogueHistory
        .filter((d: any) => d.transcript)
        .map((d: any) => d.transcript);
      if (userLines.length > 0) {
        rawTranscript = userLines.join(' ');
      }
    }

    const narrative = bc.latestTranscript || rawTranscript;
    const duration = `0${Math.max(1, Math.min(5, Math.ceil((bc.dialogueTurnsCount || 1) * 0.4)))}:15`;

    let slot = null;
    if (bc.clinicReferral && bc.clinicReferral.scheduledSlot) {
      try {
        const slotDate = new Date(bc.clinicReferral.scheduledSlot);
        slot = {
          date: slotDate.toISOString().split('T')[0],
          time: '১০:০০ AM',
          counselor: bc.clinicReferral.clinicName || 'প্যানেল আইনজীবী',
          notes: 'ক্লিনিক অ্যাপয়েন্টমেন্ট সময়সূচী বরাদ্দ'
        };
      } catch {
        // ignore
      }
    }

    return {
      id: bc.id,
      reference: bc.caseNumber || `ODH-2026-${bc.id.slice(0, 6).toUpperCase()}`,
      category: bc.category,
      categoryDisplay: categoryBn,
      district: ed.district?.value || bc.district || 'ঢাকা',
      status: tab,
      statusDisplay: statusDisplay,
      isUrgent: isUrgent,
      isApproved: isApproved,
      isReslot: isReslot,
      categoryTab: tab,
      categoryTabBn: tabBn,
      narrative: narrative,
      audioDuration: duration,
      rawTranscript: rawTranscript,
      fields: fields,
      draftComplaintText: bc.draftComplaintText || '',
      audioUrl: bc.audioUrl || null,
      dialogueHistory: bc.dialogueHistory || [],
      scheduledSlot: slot
    };
  }

  mapCategoryToBn(code: string): string {
    const map: Record<string, string> = {
      'DOWER_MAINTENANCE': 'দেনমোহর / ভরণপোষণ',
      'DOWRY': 'যৌতুক ও পারিবারিক সহিংসতা',
      'DOWRY_DOMESTIC_VIOLENCE': 'যৌতুক / পারিবারিক সহিংসতা',
      'UNPAID_WAGES': 'বকেয়া মজুরি / শ্রম',
      'LABOUR_WAGE': 'বকেয়া মজুরি / শ্রম',
      'LAND_DISPUTE': 'জমি / সম্পত্তি বিরোধ',
      'LAND_PROPERTY': 'জমি / সম্পত্তি বিরোধ',
      'WOMEN_CHILD_PROTECTION': 'নারী ও শিশু সুরক্ষা',
      'DOMESTIC_VIOLENCE': 'পারিবারিক সহিংসতা',
      'PHYSICAL_ASSAULT': 'শারীরিক আক্রমণ',
      'UNKNOWN': 'অন্যান্য আইনি সমস্যা',
      'OTHER': 'অন্য সমস্যা'
    };
    return map[code] || code || 'পারিবারিক বিরোধ';
  }

  generateDefaultComplaintText(c: ParalegalCase): string {
    const applicant = c.fields?.find(f => f.key === 'applicant')?.value || 'আবেদনকারী নাগরিক';
    const respondent = c.fields?.find(f => f.key === 'respondent')?.value || 'বিবাদী পক্ষ';
    const district = c.district || 'ঢাকা';
    const caseType = c.fields?.find(f => f.key === 'caseType')?.value || c.categoryDisplay;
    const marriageDate = c.fields?.find(f => f.key === 'marriageDate')?.value || '';
    const amount = c.fields?.find(f => f.key.includes('Amount'))?.value || '';

    return `বরাবর,
শাখা ব্যবস্থাপক, আইন সহায়তা কেন্দ্র
জেলা: ${district}।

বিষয়: ${caseType} ও আইনানুগ প্রতিকার প্রসঙ্গে আবেদন।

জনাব,
বিনীত নিবেদন এই যে, আমি ${applicant}, জেলা: ${district}। ${marriageDate ? `গত ${marriageDate} তারিখে ` : ''}${respondent}-এর সহিত বিরোধ ও আইনি সংকটের সৃষ্টি হয়।

ঘটনার বিবরণ (ক্লায়েন্টের বক্তব্য):
${c.rawTranscript || c.narrative}

${amount && amount !== 'তথ্য পাওয়া যায়নি' ? `দাবিকৃত পাওনা/ক্ষতিপূরণ বাবদ মোট ${amount} অদ্যাবধি অপরিশোধিত রহিয়াছে।\n` : ''}
অতএব, বিনীত নিবেদন এই যে, বিষয়টি পর্যালোচনা করিয়া প্রয়োজনীয় আইনানুগ ব্যবস্থা গ্রহণ করিতে আপনার সদয় হস্তক্ষেপ কামনা করিতেছি।`;
  }
}

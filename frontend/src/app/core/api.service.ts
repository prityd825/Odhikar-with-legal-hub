import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ChatResponseDto,
  GeneratedCaseSummaryDto,
  GeneratedApplicationDto,
  ParentCategoryDto,
  LegalAidClinic,
  ApplicationTemplateDto,
} from './intake.models';

const API_URL =
  typeof window !== 'undefined' && window.localStorage.getItem('odhikar_api_url')
    ? window.localStorage.getItem('odhikar_api_url')!
    : environment.apiUrl;

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);

  /**
   * Retrieves all active parent incident categories.
   */
  getParentCategories(): Observable<ParentCategoryDto[]> {
    return this.http.get<ParentCategoryDto[]>(`${API_URL}/parent-categories`);
  }

  /**
   * Retrieves prioritized cases for the paralegal review queue.
   */
  getParalegalCases(): Observable<any> {
    return this.http.get(`${API_URL}/paralegal/cases`);
  }

  /**
   * Updates case draft complaint text.
   */
  updateDraft(id: string, draftText: string): Observable<any> {
    return this.http.put(`${API_URL}/paralegal/cases/${id}/draft`, {
      draftText,
    });
  }

  /**
   * Persists paralegal-edited case summary structured fields and draft text.
   */
  updateCaseSummary(
    id: string,
    payload: { fields?: Record<string, string>; draftText?: string },
  ): Observable<any> {
    return this.http.put(`${API_URL}/paralegal/cases/${id}/summary`, payload);
  }

  /**
   * Updates case status (e.g. APPROVED, RESOLVED, URGENT, ASSIGNED, REASSIGN_SLOT).
   */
  updateCaseStatus(id: string, status: string): Observable<any> {
    return this.http.put(`${API_URL}/paralegal/cases/${id}/status`, { status });
  }

  /**
   * Reassigns or schedules a clinic appointment slot for a case.
   */
  reassignSlot(
    id: string,
    payload: { clinicId?: string; scheduledSlot?: string | Date },
  ): Observable<any> {
    return this.http.post(
      `${API_URL}/paralegal/cases/${id}/reassign-slot`,
      payload,
    );
  }

  /**
   * Builds the backend PDF download URL for a case.
   */
  exportPdfUrl(id: string): string {
    return `${API_URL}/paralegal/cases/${id}/export-pdf`;
  }

  /**
   * Unified multi-turn conversational AI turn (text or voice) powered by backend orchestrator.
   */
  sendChatMessage(formData: FormData): Observable<ChatResponseDto> {
    return this.http.post<ChatResponseDto>(`${API_URL}/chat/message`, formData);
  }

  /**
   * Generate or re-generate case summary for a conversation on demand.
   */
  generateCaseSummary(
    conversationId: string,
  ): Observable<GeneratedCaseSummaryDto> {
    return this.http.post<GeneratedCaseSummaryDto>(
      `${API_URL}/case-summaries/generate/${conversationId}`,
      {},
    );
  }

  /**
   * Generate or re-generate application draft for a conversation on demand.
   */
  generateApplicationDraft(
    conversationId: string,
  ): Observable<GeneratedApplicationDto> {
    return this.http.post<GeneratedApplicationDto>(
      `${API_URL}/application-drafts/generate/${conversationId}`,
      {},
    );
  }

  /**
   * Fetch dynamic template-based structured case summary for a case from MongoDB.
   */
  getCaseSummaryFromTemplate(caseId: string): Observable<any> {
    return this.http.get(`${API_URL}/paralegal/cases/${caseId}/summary`);
  }

  /**
   * Fetch legal aid clinics from DB.
   */
  getClinics(district?: string): Observable<LegalAidClinic[]> {
    const params: any = {};
    if (district) {
      params.district = district;
    }
    return this.http.get<LegalAidClinic[]>(`${API_URL}/paralegal/clinics`, {
      params,
    });
  }

  /**
   * Retrieves all official application templates from MongoDB application_templates collection.
   */
  getApplicationTemplates(): Observable<ApplicationTemplateDto[]> {
    return this.http.get<ApplicationTemplateDto[]>(
      `${API_URL}/application-templates`,
    );
  }

  /**
   * Retrieves active questions from MongoDB Question Bank by parent incident category code.
   */
  getQuestionBankByParent(parentCategoryCode: string): Observable<any[]> {
    return this.http.get<any[]>(
      `${API_URL}/question-bank/by-parent/${encodeURIComponent(parentCategoryCode)}`,
    );
  }

  /**
   * Retrieves all normalized answers and dictionary for a legal case.
   */
  getCaseAnswers(caseId: string): Observable<any> {
    return this.http.get<any>(
      `${API_URL}/conversation-answers/case/${encodeURIComponent(caseId)}`,
    );
  }
}

export interface ChatTurn {
  role: 'user' | 'model';
  text: string;
}

export interface ConversationRequestDto {
  incidentType: string;
  currentInput: string;
  history: ChatTurn[];
}

export interface InferredField {
  value: string | null;
  isInferred: boolean;
}

export interface ExtractedLegalData {
  applicant: InferredField | null;
  respondent: InferredField | null;
  marriageOrIncidentDate: InferredField | null;
  disputedAmount: InferredField | null;
  evidenceList: string[];
}

export interface GeneratedCaseSummaryFieldDto {
  key: string;
  labelBn: string;
  labelEn?: string | null;
  order: number;
  isRequired: boolean;
  fieldType: string;
  value: string;
  status: string; // 'Stated' | 'Inferred' | 'Missing'
  isInferred: boolean;
  inferenceNote?: string | null;
  confidence?: number | null;
  sourceKey?: string | null;
}

export interface GeneratedCaseSummaryDto {
  id: string;
  conversationId: string;
  incidentCategoryCode: string;
  incidentTypeCode?: string | null;
  templateCode: string;
  templateVersion: number;
  structuredContent: GeneratedCaseSummaryFieldDto[];
  generatedText: string;
  status: string; // 'DRAFT' | 'REVIEWED' | 'APPROVED'
  completionRate: number;
  totalRequiredFields: number;
  completedRequiredFields: number;
  missingRequiredFields: string[];
  createdAt: string;
  updatedAt: string;
}

export interface GeneratedApplicationDto {
  id: string;
  conversationId: string;
  caseSummaryId?: string | null;
  incidentCategoryCode: string;
  incidentTypeCode?: string | null;
  templateCode: string;
  subject: string;
  applicationText: string;
  status: string; // 'DRAFT'
  reviewStatus: string; // 'PENDING_REVIEW'
  createdAt: string;
  updatedAt: string;
  templateTitleBn?: string | null;
  recipientAuthorityBn?: string | null;
  disclaimerNotice?: string | null;
  placeholdersUsed?: Record<string, string> | null;
  missingPlaceholders?: string[] | null;
  reviewedBy?: string | null;
  reviewNotes?: string | null;
  reviewedAt?: string | null;
}

export interface TemplatePlaceholderDto {
  placeholder: string;
  key: string;
  labelBn: string;
  fallbackBn?: string;
  isRequired?: boolean;
  aliases?: string[];
}

export interface ApplicationTemplateDto {
  _id?: string;
  id?: string;
  categoryCode: string;
  titleBn: string;
  titleEn?: string | null;
  recipientAuthorityBn: string;
  subjectBn: string;
  bodyTemplateBn: string;
  supportedPlaceholders?: TemplatePlaceholderDto[];
  disclaimerNoticeBn?: string;
  version?: number;
  isActive?: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface AlertInformationDto {
  isActive: boolean;
  alertType?: string | null;
  severity?: string | null;
  title?: string | null;
  message?: string | null;
  matchedSignal?: string | null;
  helpline?: string | null;
}

export interface ChatResponseDto {
  conversationId: string;
  assistantMessage: string;
  followUpQuestion?: string | null;
  userTranscript?: string | null;
  inputMessageType: 'Text' | 'Voice';
  isFollowUpQuestion: boolean;
  isComplete: boolean;
  isSafetyEscalated: boolean;
  safetyReason?: string | null;
  category: string;
  extractedData?: ExtractedLegalData | null;
  timestamp: string;
  caseId?: string | null;
  caseNumber?: string | null;
  status?: string | null;
  alertInformation?: AlertInformationDto | null;
  generatedCaseSummary?: GeneratedCaseSummaryDto | null;
  generatedApplication?: GeneratedApplicationDto | null;
  totalQuestions?: number | null;
  currentQuestionIndex?: number | null;
}

export interface ExtractionResponseDto {
  isSafetyEscalated: boolean;
  safetyReason: string | null;
  isComplete: boolean;
  category: string;
  nextFollowUpQuestion: string | null;
  extractedData: ExtractedLegalData;
}

export interface IntakeTurnResponse {
  caseId: string;
  caseNumber: string;
  category: string;
  categoryConfidence: number;
  transcript: string;
  systemPrompt: string;
  isSafetyEscalated: boolean;
  safetyReason: string | null;
  awaitingMoreInfo: boolean;
  extractedData?: Record<string, { value: string | null; isInferred: boolean }>;
}

export interface ChatMessage {
  messageType?: 'Text' | 'Voice';
  id: string;
  role: 'user' | 'model';
  text: string;
  timestamp: Date;
  isAudioPlaying?: boolean;
  followUpQuestion?: string | null;
}

export interface ChatMessageDto {
  id: string;
  role: string;
  messageType: string;
  text: string;
  audioUrl?: string | null;
  timestamp: string;
}

export interface ConversationDetailDto {
  id: string;
  incidentType: string;
  status: string;
  isComplete: boolean;
  isSafetyEscalated: boolean;
  safetyReason?: string | null;
  category: string;
  extractedData?: ExtractedLegalData | null;
  messages: ChatMessageDto[];
  createdAt: string;
  updatedAt: string;
}

export interface ParentCategoryDto {
  id: string;
  code: string;
  name: string;
  nameEn?: string | null;
  description?: string | null;
  icon?: string | null;
  order: number;
  isActive: boolean;
}

export interface IncidentTypeViewModel {
  id: string;
  code: string;
  name: string;
  nameEn?: string | null;
  category: string;
  parentCategoryCode?: string | null;
  isActive: boolean;
}

export interface ParentCategoryWithIncidentsDto extends ParentCategoryDto {
  incidentTypes: IncidentTypeViewModel[];
}

export interface LegalAidClinic {
  id?: string;
  _id?: string;
  name?: string;
  nameBn?: string;
  branchName?: string;
  branchBn?: string;
  district?: string;
  districtBn?: string;
  address?: string;
  addressBn?: string;
  contactNumber?: string;
  phone?: string;
  schedule?: string;
  scheduleBn?: string;
  operatingHours?: string;
  visitingHoursBn?: string;
  availableSlots?: string[];
  availableSlotsBn?: string[];
  isActive?: boolean;
}

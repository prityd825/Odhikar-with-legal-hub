export interface CaseField {
  key: string;
  label: string;
  value: string;
  badge?: 'high-confidence' | 'inferred' | 'none-raised' | 'verified' | null;
  badgeText?: string;
  isInferred?: boolean;
}

export interface ScheduledSlot {
  date: string;
  time: string;
  counselor: string;
  notes?: string;
}

export interface QuestionBankItemDto {
  questionId: string;
  category: string;
  questionBangla: string;
  parentCategoryCodes?: string[];
  requiredFactKey?: string;
  priority?: number;
}

export interface QuestionWiseReply {
  questionNumber: number;
  questionId?: string;
  category?: string;
  requiredFactKey?: string;
  question: string;
  reply: string;
  isAnswered: boolean;
  isVoice?: boolean;
}

export interface LegalChecklistItem {
  id: string;
  category: 'jurisdiction' | 'evidence' | 'adr' | 'compliance';
  title: string;
  description: string;
  statutoryBasis: string;
  isChecked: boolean;
  isMandatory: boolean;
}

export function getDefaultLegalChecklist(category?: string): LegalChecklistItem[] {
  return [
    {
      id: 'chk-jurisdiction',
      category: 'jurisdiction',
      title: 'আঞ্চলিক ও বিষয়ভিত্তিক এখতিয়ার যাচাই (Territorial Jurisdiction)',
      description: 'ঘটনার স্থান বা বিবাদীর স্থায়ী/বর্তমান বাসস্থান অনুযায়ী সংশ্লিষ্ট পারিবারিক আদালত বা ম্যাজিস্ট্রেট আদালত এখতিয়ারভুক্ত।',
      statutoryBasis: 'পারিবারিক আদালত আইন ২০২৩, ধারা ৪ ও ৫',
      isChecked: true,
      isMandatory: true
    },
    {
      id: 'chk-limitation',
      category: 'jurisdiction',
      title: 'তামাদি মেয়াদ ও কার্যকারণ নিরীক্ষা (Statutory Limitation Audit)',
      description: 'ঘটনার সংঘটন তারিখ হতে তামাদি আইন ১৯০৮ অনুযায়ী নির্ধারিত মেয়াদের মধ্যে প্রতিকার প্রার্থনা করা হয়েছে কি না।',
      statutoryBasis: 'Limitation Act 1908 (Schedule 1)',
      isChecked: true,
      isMandatory: true
    },
    {
      id: 'chk-cause',
      category: 'compliance',
      title: 'সুনির্দিষ্ট আইনি ধারা ও মামলার সারবত্তা (Prima Facie Merit)',
      description: 'দাবিকৃত দেনমোহর, ভরণপোষণ, যৌতুক নিরোধ বা পারিবারিক সহিংসতার প্রযোজ্য ধারাসমূহ সঠিকভাবে চিহ্নিত।',
      statutoryBasis: 'যৌতুক নিরোধ ২০১৮ / দণ্ডবিধি ১৮৬০',
      isChecked: true,
      isMandatory: true
    },
    {
      id: 'chk-marriage-doc',
      category: 'evidence',
      title: 'বিবাহের মূল কাবিননামা / রেজিস্ট্রি দলিল যাচাই (Nikahnama Copy)',
      description: 'দেনমোহর ও ভরণপোষণের দাবির সমর্থনে নিকাহনামা/তালাক নোটিশের সত্যায়িত অনুলিপি বা তলবযোগ্যতার রেকর্ড প্রস্তুত।',
      statutoryBasis: 'মুসলিম বিবাহ ও তালাক (নিবন্ধন) আইন ১৯৭৪',
      isChecked: true,
      isMandatory: false
    },
    {
      id: 'chk-nid-doc',
      category: 'evidence',
      title: 'আবেদনকারীর জাতীয় পরিচয়পত্র / জন্মনিবন্ধন কপি (NID Copy)',
      description: 'আবেদনকারীর পরিচিতি, স্থায়ী ঠিকানা ও নাগরিক পরিচিতি নম্বর এনআইডি সার্ভার যাচাইযোগ্য।',
      statutoryBasis: 'নাগরিক প্রমাণ ও সনাক্তকরণ বিধিমালা',
      isChecked: true,
      isMandatory: true
    },
    {
      id: 'chk-medical',
      category: 'evidence',
      title: 'মেডিকেল ইনজুরি সার্টিফিকেট / চিকিৎসকের প্রেসক্রিপশন',
      description: 'শারীরিক নির্যাতন সংক্রান্ত অপরাধে ভিকটিম সাপোর্ট বা সরকারি হাসপাতালের চিকিৎসা সনদ পর্যালোচনা।',
      statutoryBasis: 'নারী ও শিশু নির্যাতন দমন আইন ২০০০, ধারা ৩২',
      isChecked: false,
      isMandatory: false
    },
    {
      id: 'chk-gd',
      category: 'evidence',
      title: 'থানা জিডি / পুলিশ অভিযোগ বা প্রাথমিক রিপোর্ট (GD / FIR)',
      description: 'নিরাপত্তা ঝুঁকি বা পূর্ববর্তী শারীরিক হুমকির বিষয়ে সংশ্লিষ্ট থানায় সাধারণ ডায়েরি (GD) হয়েছে কি না।',
      statutoryBasis: 'পুলিশ রেগুলেশন বেঙ্গল (PRB)',
      isChecked: true,
      isMandatory: false
    },
    {
      id: 'chk-adr',
      category: 'adr',
      title: 'প্রি-কেইস লিগ্যাল এইড মিডিয়েশন (ADR) পরীক্ষা',
      description: 'আদালতে মোকদ্দমা দায়েরের পূর্বে জেলা লিগ্যাল এইড অফিসারের মাধ্যমে মধ্যস্থতার আইনগত সুযোগ পরীক্ষা করা হয়েছে।',
      statutoryBasis: 'আইনগত সহায়তা প্রদান আইন ২০০০, ধারা ২১ক',
      isChecked: true,
      isMandatory: true
    },
    {
      id: 'chk-protection-order',
      category: 'adr',
      title: 'জরুরি সুরক্ষা আদেশ (Interim Protection Order)',
      description: 'পারিবারিক সহিংসতা থেকে তাৎক্ষণিক সুরক্ষা পেতে জরুরি বাসস্থান বা অন্তর্বর্তীকালীন নিষেধাজ্ঞা প্রয়োজন কি না।',
      statutoryBasis: 'পারিবারিক সহিংসতা (সুরক্ষা) আইন ২০১০, ধারা ১১-১৪',
      isChecked: false,
      isMandatory: false
    }
  ];
}

export interface ParalegalCase {
  id: string;
  reference: string;
  category: string;
  categoryDisplay: string;
  district: string;
  status: string;
  statusDisplay: string;
  isUrgent: boolean;
  isSafetyEscalated?: boolean;
  safetyReason?: string;
  narrative: string;
  audioDuration: string;
  rawTranscript: string;
  fields: CaseField[];
  templateTitle?: string;
  completionRate?: number;
  scheduledSlot?: ScheduledSlot | null;
  isApproved?: boolean;
  isReslot?: boolean;
  categoryTab?: string;
  categoryTabBn?: string;
  draftComplaintText?: string | null;
  audioUrl?: string | null;
  dialogueHistory?: any[];
  legalChecklist?: LegalChecklistItem[];
  advocateNotes?: string;
}

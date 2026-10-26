export interface IncidentDefinition {
  code: string;
  bn: string;
  en: string;
  description: string;
  descriptionEn?: string;
  rightShort: string;
  rightShortEn?: string;
  icon?: string;
}

export const INCIDENTS: IncidentDefinition[] = [
  {
    code: 'DOWER_MAINTENANCE',
    bn: 'দেনমোহর / ভরণপোষণ',
    en: 'Dower & Maintenance',
    description: 'দেনমোহর, ভরণপোষণ বা বিবাহ-পরবর্তী পাওনা',
    descriptionEn: 'Dower claim, child maintenance, post-divorce support',
    rightShort: 'দেনমোহর স্ত্রীর আইনি অধিকার এবং স্ত্রী ও সন্তানের নিয়মিত খোরপোষ পাওয়ার বিধান রয়েছে।',
    rightShortEn: 'Dower is an absolute legal right of the wife, with statutory rights to child maintenance.',
    icon: 'scale'
  },
  {
    code: 'DOWRY_DOMESTIC_VIOLENCE',
    bn: 'যৌতুক / পারিবারিক সহিংসতা',
    en: 'Dowry & Domestic Violence',
    description: 'যৌতুক দাবি, শারীরিক বা মানসিক নির্যাতন ও সুরক্ষা',
    descriptionEn: 'Dowry harassment, physical or psychological torture & protection',
    rightShort: 'যৌতুক দাবি আইনত দণ্ডনীয় অপরাধ এবং পারিবারিক সহিংসতায় তাৎক্ষণিক সুরক্ষা আদেশ পাওয়ার অধিকার রয়েছে।',
    rightShortEn: 'Dowry demands are non-bailable criminal offenses; immediate statutory protection orders apply.',
    icon: 'shield-alert'
  },
  {
    code: 'LABOUR_WAGE',
    bn: 'বকেয়া মজুরি / শ্রম',
    en: 'Labour & Unpaid Wages',
    description: 'মজুরি, ওভারটাইম, সার্ভিস বেনিফিট বা বেআইনি বরখাস্ত',
    descriptionEn: 'Unpaid salary, overtime arrears, unlawful termination & severance',
    rightShort: 'শ্রম আইন অনুযায়ী বকেয়া মজুরি ও ক্ষতিপূরণ আদায়ে শ্রম আদালতে যাওয়ার অধিকার রয়েছে।',
    rightShortEn: 'Labour Act guarantees right to recover wage arrears and statutory compensation.',
    icon: 'briefcase'
  },
  {
    code: 'LAND_PROPERTY',
    bn: 'জমি / সম্পত্তি বিরোধ',
    en: 'Land & Property Dispute',
    description: 'পৈতৃক সম্পত্তি থেকে বঞ্চনা, দখল বা দলিল বিরোধ',
    descriptionEn: 'Deprivation of inheritance, land grabbing, and title disputes',
    rightShort: 'আইনসম্মত মালিকানা ও দখল বজায় রাখা এবং নারী উত্তরাধিকারীদের সম্পত্তির সুরক্ষার অধিকার রয়েছে।',
    rightShortEn: 'Protection of legal ownership, peaceful possession and female inheritance shares.',
    icon: 'home'
  },
  {
    code: 'WOMEN_CHILD_PROTECTION',
    bn: 'নারী ও শিশু সুরক্ষা',
    en: 'Women & Child Protection',
    description: 'সন্তানের অভিভাবকত্ব ও হেফাজত, নিরাপত্তাহীনতা বা সংকট',
    descriptionEn: 'Child custody, guardianship, child safety and emergency shelter',
    rightShort: 'নারী ও শিশুর সুরক্ষায় কঠোর আইন ও বিনামূল্যে সরকারি লিগ্যাল এইড পাওয়ার অধিকার রয়েছে।',
    rightShortEn: 'Strict protective statutes and free government legal aid available for women & children.',
    icon: 'users'
  },
  {
    code: 'OTHER',
    bn: 'অন্য সমস্যা',
    en: 'Other Legal Issues',
    description: 'বিবাহ প্রতারণা, চুক্তিভঙ্গ বা অন্যান্য সাধারণ আইনি বিষয়',
    descriptionEn: 'Contractual disputes, marital fraud, or general civil grievances',
    rightShort: 'আইনের দৃষ্টিতে যেকোনো বিরোধে সঠিক আইনি পরামর্শ ও বিচার পাওয়ার অধিকার রয়েছে।',
    rightShortEn: 'Right to legal representation, preliminary legal advice, and fair statutory justice.',
    icon: 'help-circle'
  }
];

export const RULES: Record<string, { titleBn?: string; rights: string[]; next: string[]; template: string }> = {
  DOWER_MAINTENANCE: {
    titleBn: 'দেনমোহর ও খোরপোষ সংক্রান্ত অধিকার',
    rights: [
      'দেনমোহর স্ত্রীর আইনগত ও ধর্মীয় অধিকার, যা স্বামী পরিশোধ করতে বাধ্য।',
      'বিবাহ বহাল থাকা বা বিচ্ছেদের পর স্ত্রী ও সন্তানের নিয়মিত খোরপোষ পাওয়ার আইনগত অধিকার রয়েছে।'
    ],
    next: [
      'নিকাহনামা ও দেনমোহরের নথিপত্র সংগ্রহ করুন।',
      'প্রয়োজনে পারিবারিক আদালতে মামলা দায়েরের জন্য আইনগত পরামর্শ নিন।'
    ],
    template: 'দেনমোহর ও খোরপোষ সংক্রান্ত আবেদন\n\nঘটনার বিবরণ: {{narrative}}\nদাবি: দেনমোহর ও বকেয়া খোরপোষ আদায়।'
  },
  DOWRY_DOMESTIC_VIOLENCE: {
    titleBn: 'যৌতুক ও পারিবারিক সহিংসতা প্রতিরোধ',
    rights: [
      'যৌতুক দাবি করা বা গ্রহণ করা আইন অনুযায়ী একটি জামিন-অযোগ্য অপরাধ।',
      'পারিবারিক সহিংসতার শিকার হলে সুরক্ষা আদেশ পাওয়ার আইনি অধিকার রয়েছে।'
    ],
    next: [
      'চিকিৎসার কাগজপত্র ও প্রমাণাদি সংরক্ষণ করুন।',
      'জরুরি পরিস্থিতিতে জাতীয় হেল্পলাইন ১০৯ অথবা ৯৯৯ অথবা ১৬৪৩০-এ ফোন করুন।'
    ],
    template: 'যৌতুক ও পারিবারিক নির্যাতন সংক্রান্ত অভিযোগ\n\nঘটনার বিবরণ: {{narrative}}\nদাবি: আইনি প্রতিকার ও সুরক্ষা।'
  },
  DOWRY: {
    titleBn: 'যৌতুক ও পারিবারিক সহিংসতা প্রতিরোধ',
    rights: [
      'যৌতুক দাবি করা বা গ্রহণ করা আইন অনুযায়ী একটি জামিন-অযোগ্য অপরাধ।',
      'পারিবারিক সহিংসতার শিকার হলে সুরক্ষা আদেশ পাওয়ার আইনি অধিকার রয়েছে।'
    ],
    next: [
      'চিকিৎসার কাগজপত্র ও প্রমাণাদি সংরক্ষণ করুন।',
      'জরুরি পরিস্থিতিতে জাতীয় হেল্পলাইন ১০৯ অথবা ১৬৪৩০-এ ফোন করুন।'
    ],
    template: 'যৌতুক ও পারিবারিক নির্যাতন সংক্রান্ত অভিযোগ\n\nঘটনার বিবরণ: {{narrative}}\nদাবি: আইনি প্রতিকার ও সুরক্ষা।'
  },
  LABOUR_WAGE: {
    titleBn: 'বকেয়া মজুরি ও শ্রম অধিকার',
    rights: [
      'শ্রম আইন অনুযায়ী সময়মতো নির্ধারিত মজুরি পাওয়া প্রতিটি শ্রমিকের অধিকার।',
      'অন্যায়ভাবে বরখাস্ত বা বেতন বন্ধ করলে ক্ষতিপূরণ পাওয়ার বিধান রয়েছে।'
    ],
    next: [
      'চাকরির পরিচয়পত্র, হাজিরা খাতা বা পে-স্লিপ সংরক্ষণ করুন।',
      'শ্রম আদালতে বা শ্রম অধিদপ্তরে অভিযোগ দায়ের করুন।'
    ],
    template: 'বকেয়া বেতন আদায়ের আবেদন\n\nঘটনার বিবরণ: {{narrative}}\nদাবি: বকেয়া পাওনা ও ক্ষতিপূরণ পরিশোধ।'
  },
  UNPAID_WAGES: {
    titleBn: 'বকেয়া মজুরি ও শ্রম অধিকার',
    rights: [
      'শ্রম আইন অনুযায়ী সময়মতো নির্ধারিত মজুরি পাওয়া প্রতিটি শ্রমিকের অধিকার।',
      'অন্যায়ভাবে বরখাস্ত বা বেতন বন্ধ করলে ক্ষতিপূরণ পাওয়ার বিধান রয়েছে।'
    ],
    next: [
      'চাকরির পরিচয়পত্র, হাজিরা খাতা বা পে-স্লিপ সংরক্ষণ করুন।',
      'শ্রম আদালতে বা শ্রম অধিদপ্তরে অভিযোগ দায়ের করুন।'
    ],
    template: 'বকেয়া বেতন আদায়ের আবেদন\n\nঘটনার বিবরণ: {{narrative}}\nদাবি: বকেয়া পাওনা ও ক্ষতিপূরণ পরিশোধ।'
  },
  LAND_PROPERTY: {
    titleBn: 'জমি ও সম্পত্তি বিরোধ',
    rights: [
      'বৈধ মালিকানা ও দখল থাকা অবস্থায় কাউকে বেআইনিভাবে জমি থেকে উচ্ছেদ করা যাবে না।',
      'জমি সংক্রান্ত বিরোধের ক্ষেত্রে সহকারী কমিশনার (ভূমি) বা দেওয়ানি আদালতে প্রতিকার পাওয়া যায়।'
    ],
    next: [
      'খতিয়ান, পরচা, দলিল ও নামজারি কাগজ প্রস্তুত রাখুন।',
      'শান্তিশৃঙ্খলা ভঙ্গের আশঙ্কা থাকলে ১৪৪/১৪৫ ধারায় আবেদন করুন।'
    ],
    template: 'জমি বিরোধ সংক্রান্ত অভিযোগ\n\nঘটনার বিবরণ: {{narrative}}\nদাবি: সীমানা নির্ধারণ ও দখল বজায় রাখা।'
  },
  LAND_DISPUTE: {
    titleBn: 'জমি ও সম্পত্তি বিরোধ',
    rights: [
      'বৈধ মালিকানা ও দখল থাকা অবস্থায় কাউকে বেআইনিভাবে জমি থেকে উচ্ছেদ করা যাবে না।',
      'জমি সংক্রান্ত বিরোধের ক্ষেত্রে সহকারী কমিশনার (ভূমি) বা দেওয়ানি আদালতে প্রতিকার পাওয়া যায়।'
    ],
    next: [
      'খতিয়ান, পরচা, দলিল ও নামজারি কাগজ প্রস্তুত রাখুন।',
      'শান্তিশৃঙ্খলা ভঙ্গের আশঙ্কা থাকলে ১৪৪/১৪৫ ধারায় আবেদন করুন।'
    ],
    template: 'জমি বিরোধ সংক্রান্ত অভিযোগ\n\nঘটনার বিবরণ: {{narrative}}\nদাবি: সীমানা নির্ধারণ ও দখল বজায় রাখা।'
  },
  WOMEN_CHILD_PROTECTION: {
    titleBn: 'নারী ও শিশু সুরক্ষা',
    rights: [
      'নারী ও শিশুর শারীরিক ও মানসিক সুরক্ষায় কঠোর আইন বিদ্যমান।',
      'ভুক্তভোগী বিনা খরচে সরকারি আইনি সহায়তা (লিগ্যাল এইড) পাওয়ার অধিকারী।'
    ],
    next: [
      'নিকটস্থ থানায় যোগাযোগ করুন বা ওয়ান-স্টপ ক্রাইসিস সেন্টারের সহায়তা নিন।',
      'জরুরি সহায়তার জন্য ৯৯৯ অথবা ১০৯-এ ফোন করুন।'
    ],
    template: 'নারী ও শিশু সুরক্ষা আবেদন\n\nঘটনার বিবরণ: {{narrative}}\nদাবি: নিরাপত্তা নিশ্চিতকরণ ও আইনি ব্যবস্থা।'
  },
  WOMEN_CHILDREN: {
    titleBn: 'নারী ও শিশু সুরক্ষা',
    rights: [
      'নারী ও শিশুর শারীরিক ও মানসিক সুরক্ষায় কঠোর আইন বিদ্যমান।',
      'ভুক্তভোগী বিনা খরচে সরকারি আইনি সহায়তা (লিগ্যাল এইড) পাওয়ার অধিকারী।'
    ],
    next: [
      'নিকটস্থ থানায় যোগাযোগ করুন বা ওয়ান-স্টপ ক্রাইসিস সেন্টারের সহায়তা নিন।',
      'জরুরি সহায়তার জন্য ৯৯৯ অথবা ১০৯-এ ফোন করুন।'
    ],
    template: 'নারী ও শিশু সুরক্ষা আবেদন\n\nঘটনার বিবরণ: {{narrative}}\nদাবি: নিরাপত্তা নিশ্চিতকরণ ও আইনি ব্যবস্থা।'
  },
  OTHER: {
    titleBn: 'সাধারণ আইনি সমস্যা',
    rights: [
      'আইনের দৃষ্টিতে সকল নাগরিকের ন্যায়বিচার ও আইনি পরামর্শ পাওয়ার অধিকার রয়েছে।'
    ],
    next: [
      'ঘটনার প্রমাণাদি সংগ্রহ করুন।',
      'নিকটস্থ লিগ্যাল এইড অফিসে যোগাযোগ করুন।'
    ],
    template: 'আইনি সহায়তা আবেদন\n\nঘটনার বিবরণ: {{narrative}}\nদাবি: সঠিক ফোরামে আইনি সমাধান।'
  },
  OUT_OF_SCOPE: {
    titleBn: 'সাধারণ আইনি সমস্যা',
    rights: [
      'আইনের দৃষ্টিতে সকল নাগরিকের ন্যায়বিচার ও আইনি পরামর্শ পাওয়ার অধিকার রয়েছে।'
    ],
    next: [
      'ঘটনার প্রমাণাদি সংগ্রহ করুন।',
      'নিকটস্থ লিগ্যাল এইড অফিসে যোগাযোগ করুন।'
    ],
    template: 'আইনি সহায়তা আবেদন\n\nঘটনার বিবরণ: {{narrative}}\nদাবি: সঠিক ফোরামে আইনি সমাধান।'
  }
};

export function screenSafety(text: string) {
  const s = text.toLowerCase();
  const urgent = ['মারধর', 'খুন', 'হত্যা', 'অস্ত্র', 'দাঙ্গা', 'suicide', 'kill', 'weapon', 'rape', 'আক্রমণ', 'হুমকি'].some((x) => s.includes(x));
  return { urgent, status: urgent ? 'urgent' : 'clear', helpline: urgent ? '16430' : null };
}

export function makeDraft(code: string, narrative: string) {
  return (RULES[code] ?? RULES['OTHER'] ?? RULES['OUT_OF_SCOPE']).template.replace('{{narrative}}', narrative);
}

export function inferFields(text: string) {
  return [{ key: 'narrative', value: text, isInferred: false, confidence: 1 }];
}

export type IncidentCode = keyof typeof RULES;

export function makeReference() {
  return `ODH-${new Date().getFullYear()}-${Math.random().toString(36).slice(2, 8).toUpperCase()}`;
}

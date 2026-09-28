import { ApplicationTemplateDto } from '../../core/intake.models';

export const MONGO_APPLICATION_TEMPLATES: ApplicationTemplateDto[] = [
  {
    _id: '90314ead-3fa8-433f-817a-75e1206c2e43',
    id: '90314ead-3fa8-433f-817a-75e1206c2e43',
    categoryCode: 'DOWER_MAINTENANCE',
    titleBn: 'দেনমোহর ও খোরপোষ আদায়ের পারিবারিক আদালত আবেদন',
    titleEn: 'Family Court Application for Realization of Dower & Maintenance',
    recipientAuthorityBn: 'বিজ্ঞ সহকারী জজ ও পারিবারিক আদালত, {{DISTRICT}}',
    subjectBn: 'দেনমোহর ও বকেয়া খোরপোষের টাকা আদায়ের মোকদ্দমা',
    bodyTemplateBn: `বিজ্ঞ সহকারী জজ ও পারিবারিক আদালত, {{DISTRICT}}

বিষয়: পারিবারিক আদালত আইন, ২০২৩ অনুযায়ী দেনমোহর ও খোরপোষ আদায়ের আবেদন।

আবেদনকারী:
{{APPLICANT_NAME}}
ঠিকানা: {{APPLICANT_ADDRESS}}

বনাম

বিবাদী:
{{RESPONDENT_NAME}}
ঠিকানা: {{RESPONDENT_ADDRESS}}

মাননীয় আদালতের নিকট আবেদনকারীর বিনীত নিবেদন এই যে,

১। আবেদনকারী ও বিবাদীর মধ্যে বিগত {{MARRIAGE_DATE}} তারিখে ইসলামি শরিয়ত মোতাবেক ও মুসলিম বিবাহ ও তালাক (নিবন্ধন) আইন অনুযায়ী শুভ বিবাহ সম্পন্ন হয়।

২। উক্ত বিবাহে দেনমোহর বাবদ সর্বমোট {{DOWER_TOTAL_AMOUNT}} টাকা ধার্য করা হয়। যার মধ্যে উসূল বাবদ {{DOWER_PAID_AMOUNT}} টাকা ব্যতীত অবশিষ্ট বকেয়া দেনমোহর বাবদ {{DOWER_UNPAID_AMOUNT}} টাকা বিবাদী পরিশোধ করেননি।

৩। বিবাদী বিগত বেশ কিছুদিন যাবৎ আবেদনকারীকে কোনো প্রকার খোরপোষ প্রদান না করায় বকেয়া খোরপোষ বাবদ {{MAINTENANCE_DUE}} টাকা পাওনা রয়েছে।

৪। পারিবারিক প্রেক্ষাপট ও ঘটনার সংক্ষিপ্ত বিবরণ:
{{INCIDENT_DETAILS}}

৫। দাবির সমর্থনে প্রমাণাদির তালিকা:
{{EVIDENCE_LIST}}

প্রার্থনা:
অতএব, বিনীত প্রার্থনা এই যে, বিজ্ঞ আদালত অনুগ্রহপূর্বক বিবাদীর নিকট হইতে আবেদনকারীর দেনমোহর ও বকেয়া খোরপোষ বাবদ সমুদয় অর্থ আদায়ের আদেশদানে এবং উপযুক্ত আইনানুগ প্রতিকার প্রদানে মর্জি হয়।

তারিখ: {{APPLICATION_DATE}}
নিবেদক,
{{APPLICANT_NAME}}`,
    supportedPlaceholders: [
      { placeholder: '{{APPLICANT_NAME}}', key: 'APPLICANT_NAME', labelBn: 'আবেদনকারীর নাম', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{APPLICANT_ADDRESS}}', key: 'APPLICANT_ADDRESS', labelBn: 'আবেদনকারীর ঠিকানা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{RESPONDENT_NAME}}', key: 'RESPONDENT_NAME', labelBn: 'বিবাদীর নাম', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{RESPONDENT_ADDRESS}}', key: 'RESPONDENT_ADDRESS', labelBn: 'বিবাদীর ঠিকানা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{MARRIAGE_DATE}}', key: 'MARRIAGE_DATE', labelBn: 'বিবাহের তারিখ', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{DOWER_TOTAL_AMOUNT}}', key: 'DOWER_TOTAL_AMOUNT', labelBn: 'মোট দেনমোহর', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{DOWER_PAID_AMOUNT}}', key: 'DOWER_PAID_AMOUNT', labelBn: 'পরিশোধিত দেনমোহর', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{DOWER_UNPAID_AMOUNT}}', key: 'DOWER_UNPAID_AMOUNT', labelBn: 'বকেয়া দেনমোহর', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{MAINTENANCE_DUE}}', key: 'MAINTENANCE_DUE', labelBn: 'বকেয়া খোরপোষ', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{INCIDENT_DETAILS}}', key: 'INCIDENT_DETAILS', labelBn: 'ঘটনার বিবরণ', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{EVIDENCE_LIST}}', key: 'EVIDENCE_LIST', labelBn: 'প্রমাণাদির তালিকা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{DISTRICT}}', key: 'DISTRICT', labelBn: 'জেলা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false }
    ],
    disclaimerNoticeBn: 'সতর্কীকরণ ও আইনি বিজ্ঞপ্তি (DRAFT - PENDING REVIEW): এই আবেদনপত্রটি আবেদনকারীর প্রদত্ত প্রাথমিক ও একতরফা মৌখিক/লিখিত বিবরণের ভিত্তিতে অধিকার (Odhikar) ডিজিটাল সহায়িকা কর্তৃক প্রস্তুতকৃত একটি খসড়া রূপরেখা মাত্র। এতে বর্ণিত অভিযোগ ও তথ্যাদি কোনো আদালত বা তদন্তকারী কর্তৃপক্ষ কর্তৃক চূড়ান্তভাবে যাচাই বা প্রমাণিত নয়। বিজ্ঞ আইনজীবী বা সংশ্লিষ্ট আইনি কর্মকর্তার দ্বারা তথ্যসমূহ যাচাই ও স্বাক্ষর ব্যতীত এটি চূড়ান্ত আইনগত নথি হিসেবে গণ্য হবে না।',
    version: 1,
    isActive: true
  },
  {
    _id: '0c00024f-1978-4813-835f-65da5ea53aa7',
    id: '0c00024f-1978-4813-835f-65da5ea53aa7',
    categoryCode: 'DOWRY_DOMESTIC_VIOLENCE',
    titleBn: 'যৌতুক নিরোধ ও পারিবারিক সহিংসতা সুরক্ষা অভিযোগ',
    titleEn: 'Complaint on Dowry Prohibition & Domestic Violence Protection',
    recipientAuthorityBn: 'বিজ্ঞ চিফ জুডিসিয়াল ম্যাজিস্ট্রেট / মেট্রোপলিটন ম্যাজিস্ট্রেট আদালত, {{DISTRICT}}',
    subjectBn: 'যৌতুক নিরোধ আইন, ২০১৮ এবং পারিবারিক সহিংসতা (প্রতিরোধ ও সুরক্ষা) আইন, ২০১০ অনুযায়ী প্রতিকার পাওয়ার দরখাস্ত',
    bodyTemplateBn: `বিজ্ঞ আদালত, {{DISTRICT}}

বিষয়: যৌতুক দাবি ও পারিবারিক নির্যাতনের বিরুদ্ধে আইনানুগ ব্যবস্থা গ্রহণ ও সুরক্ষার আবেদন।

অভিযোগকারী:
{{APPLICANT_NAME}}

বনাম

অভিযুক্ত:
{{RESPONDENT_NAME}}

মাননীয় আদালতের নিকট বিনীত নিবেদন এই যে,

১। অভিযোগকারী ও অভিযুক্তের বিবাহের পর হইতে অভিযুক্ত পক্ষ বিভিন্ন সময়ে বেআইনিভাবে {{DOWRY_DEMAND_AMOUNT}} টাকা যৌতুক দাবি করিয়া আসিতেছে।

২। উক্ত যৌতুকের দাবি পূরণ না করায় অভিযোগকারীর ওপর বিভিন্ন সময়ে শারীরিক ও মানসিক নির্যাতন চালানো হয় (নির্যাতনের ধরন: {{ABUSE_TYPE}})।

৩। শারীরিক আঘাত ও জখমের বিবরণ: {{INJURY_DETAILS}}।

৪। ঘটনাস্থল ও উচ্ছেদের বিবরণ: {{INCIDENT_LOCATION}}।

৫। বর্তমান নিরাপত্তা ঝুঁকি ও হুমকি: {{IMMEDIATE_THREAT}}।

৬। ঘটনার বিস্তারিত বিবরণ:
{{INCIDENT_DETAILS}}

৭। প্রমাণাদির বিবরণ: {{EVIDENCE_LIST}}।

প্রার্থনা:
অতএব, বিনীত প্রার্থনা এই যে, বিজ্ঞ আদালত অভিযোগ আমলে নিয়ে অভিযুক্তের বিরুদ্ধে যৌতুক নিরোধ আইন অনুযায়ী আইনানুগ ব্যবস্থা গ্রহণ এবং অভিযোগকারীর শারীরিক নিরাপত্তা সুরক্ষায় সুরক্ষা আদেশ প্রদানে মর্জি হয়।

তারিখ: {{APPLICATION_DATE}}
নিবেদক,
{{APPLICANT_NAME}}`,
    supportedPlaceholders: [
      { placeholder: '{{APPLICANT_NAME}}', key: 'APPLICANT_NAME', labelBn: 'অভিযোগকারীর নাম', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{RESPONDENT_NAME}}', key: 'RESPONDENT_NAME', labelBn: 'অভিযুক্তের নাম', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{DOWRY_DEMAND_AMOUNT}}', key: 'DOWRY_DEMAND_AMOUNT', labelBn: 'দাবিকৃত যৌতুক', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{ABUSE_TYPE}}', key: 'ABUSE_TYPE', labelBn: 'নির্যাতনের ধরন', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{INJURY_DETAILS}}', key: 'INJURY_DETAILS', labelBn: 'আঘাতের বিবরণ', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{INCIDENT_LOCATION}}', key: 'INCIDENT_LOCATION', labelBn: 'ঘটনাস্থল', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{IMMEDIATE_THREAT}}', key: 'IMMEDIATE_THREAT', labelBn: 'নিরাপত্তা ঝুঁকি', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{INCIDENT_DETAILS}}', key: 'INCIDENT_DETAILS', labelBn: 'ঘটনার বিবরণ', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{EVIDENCE_LIST}}', key: 'EVIDENCE_LIST', labelBn: 'প্রমাণাদির তালিকা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{DISTRICT}}', key: 'DISTRICT', labelBn: 'জেলা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false }
    ],
    disclaimerNoticeBn: 'সতর্কীকরণ ও আইনি বিজ্ঞপ্তি (DRAFT - PENDING REVIEW): এই আবেদনপত্রটি আবেদনকারীর প্রদত্ত প্রাথমিক ও একতরফা মৌখিক/লিখিত বিবরণের ভিত্তিতে অধিকার (Odhikar) ডিজিটাল সহায়িকা কর্তৃক প্রস্তুতকৃত একটি খসড়া রূপরেখা মাত্র। এতে বর্ণিত অভিযোগ ও তথ্যাদি কোনো আদালত বা তদন্তকারী কর্তৃপক্ষ কর্তৃক চূড়ান্তভাবে যাচাই বা প্রমাণিত নয়। বিজ্ঞ আইনজীবী বা সংশ্লিষ্ট আইনি কর্মকর্তার দ্বারা তথ্যসমূহ যাচাই ও স্বাক্ষর ব্যতীত এটি চূড়ান্ত আইনগত নথি হিসেবে গণ্য হবে না।',
    version: 1,
    isActive: true
  },
  {
    _id: '6857023c-8cab-466b-953b-8d0ef3accde6',
    id: '6857023c-8cab-466b-953b-8d0ef3accde6',
    categoryCode: 'LABOUR_WAGE',
    titleBn: 'বকেয়া মজুরি ও শ্রম পাওনা আদায়ের আবেদন',
    titleEn: 'Claim Application for Recovery of Unpaid Wages & Benefits',
    recipientAuthorityBn: 'বিজ্ঞ চেয়ারম্যান, শ্রম আদালত / উপ-মহাপরিদর্শক, কলকারখানা ও প্রতিষ্ঠান পরিদর্শন অধিদপ্তর, {{DISTRICT}}',
    subjectBn: 'বাংলাদেশ শ্রম আইন, ২০০৬ এর ৩৩ ধারা অনুযায়ী বকেয়া মজুরি ও আইনানুগ পাওনা আদায়ের আবেদন',
    bodyTemplateBn: `বিজ্ঞ শ্রম আদালত / শ্রম কর্মকর্তা, {{DISTRICT}}

বিষয়: বকেয়া বেতন-ভাতা ও শ্রম ক্ষতিপূরণ আদায়ের দরখাস্ত।

আবেদনকারী (শ্রমিক):
{{APPLICANT_NAME}}
পদবী: {{JOB_DESIGNATION}}

বনাম

বিবাদী (মালিক/প্রতিষ্ঠান):
{{EMPLOYER_NAME}}

মাননীয় কর্তৃপক্ষের নিকট বিনীত নিবেদন এই যে,

১। আবেদনকারী বিবাদীর প্রতিষ্ঠানে {{JOB_DESIGNATION}} হিসেবে মাসিক {{MONTHLY_SALARY}} টাকা বেতনে নিষ্ঠার সহিত দায়িত্ব পালন করিয়া আসিতেছিলেন।

২। বিবাদী প্রতিষ্ঠান বিগত {{UNPAID_MONTHS}} মাসের বেতন বাবদ সর্বমোট {{UNPAID_WAGE_AMOUNT}} টাকা এবং ওভারটাইম/ভাতা বাবদ {{OVERTIME_DUE}} টাকা অদ্যাবধি পরিশোধ না করিয়া অন্যায়ভাবে বকেয়া রাখিয়াছে।

৩। ঘটনার বিস্তারিত বিবরণ ও বরখাস্তের প্রেক্ষাপট:
{{INCIDENT_DETAILS}}

৪। নথিপত্র ও প্রমাণাদি: {{EVIDENCE_LIST}}।

প্রার্থনা:
অতএব, বিনীত প্রার্থনা এই যে, আবেদনকারীর আইনানুগ বকেয়া মজুরি ও ক্ষতিপূরণের টাকা আদায় করিয়া দিতে আইনগত আদেশ প্রদানে মর্জি হয়।

তারিখ: {{APPLICATION_DATE}}
নিবেদক,
{{APPLICANT_NAME}}`,
    supportedPlaceholders: [
      { placeholder: '{{APPLICANT_NAME}}', key: 'APPLICANT_NAME', labelBn: 'শ্রমিকের নাম', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{EMPLOYER_NAME}}', key: 'EMPLOYER_NAME', labelBn: 'মালিক/প্রতিষ্ঠানের নাম', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{JOB_DESIGNATION}}', key: 'JOB_DESIGNATION', labelBn: 'পদবী', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{MONTHLY_SALARY}}', key: 'MONTHLY_SALARY', labelBn: 'মাসিক বেতন', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{UNPAID_WAGE_AMOUNT}}', key: 'UNPAID_WAGE_AMOUNT', labelBn: 'বকেয়া মজুরির পরিমাণ', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{UNPAID_MONTHS}}', key: 'UNPAID_MONTHS', labelBn: 'বকেয়া মাস', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{OVERTIME_DUE}}', key: 'OVERTIME_DUE', labelBn: 'ওভারটাইম পাওনা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{INCIDENT_DETAILS}}', key: 'INCIDENT_DETAILS', labelBn: 'ঘটনার বিবরণ', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{EVIDENCE_LIST}}', key: 'EVIDENCE_LIST', labelBn: 'প্রমাণাদির তালিকা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{DISTRICT}}', key: 'DISTRICT', labelBn: 'জেলা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false }
    ],
    disclaimerNoticeBn: 'সতর্কীকরণ ও আইনি বিজ্ঞপ্তি (DRAFT - PENDING REVIEW): এই আবেদনপত্রটি আবেদনকারীর প্রদত্ত প্রাথমিক ও একতরফা মৌখিক/লিখিত বিবরণের ভিত্তিতে অধিকার (Odhikar) ডিজিটাল সহায়িকা কর্তৃক প্রস্তুতকৃত একটি খসড়া রূপরেখা মাত্র। এতে বর্ণিত অভিযোগ ও তথ্যাদি কোনো আদালত বা তদন্তকারী কর্তৃপক্ষ কর্তৃক চূড়ান্তভাবে যাচাই বা প্রমাণিত নয়। বিজ্ঞ আইনজীবী বা সংশ্লিষ্ট আইনি কর্মকর্তার দ্বারা তথ্যসমূহ যাচাই ও স্বাক্ষর ব্যতীত এটি চূড়ান্ত আইনগত নথি হিসেবে গণ্য হবে না।',
    version: 1,
    isActive: true
  },
  {
    _id: '3401c31b-cbdc-4e1f-8903-41ae79d37cd6',
    id: '3401c31b-cbdc-4e1f-8903-41ae79d37cd6',
    categoryCode: 'LAND_PROPERTY',
    titleBn: 'জমি ও সম্পত্তি বিরোধ নিরসন ও দখল রক্ষার দরখাস্ত',
    titleEn: 'Petition for Resolution of Land Dispute & Protection of Possession',
    recipientAuthorityBn: 'বিজ্ঞ অতিরিক্ত জেলা ম্যাজিস্ট্রেট আদালত / বিজ্ঞ সহকারী জজ আদালত, {{DISTRICT}}',
    subjectBn: 'ফৌজদারী কার্যবিধি ১৪৪/১৪৫ ধারা অনুযায়ী বিরোধপূর্ণ জমিতে শান্তিশৃঙ্খলা বজায় রাখা ও দখল রক্ষার দরখাস্ত',
    bodyTemplateBn: `বিজ্ঞ আদালত, {{DISTRICT}}

বিষয়: জমিতে অবৈধ অনুপ্রবেশ, জবরদখল চেষ্টা প্রতিরোধ ও প্রতিকারের আবেদন।

প্রথম পক্ষ (আবেদনকারী):
{{APPLICANT_NAME}}

বনাম

দ্বিতীয় পক্ষ (বিবাদী):
{{RESPONDENT_NAME}}

মাননীয় আদালতের নিকট বিনীত নিবেদন এই যে,

১। তফসিল বর্ণিত বিরোধপূর্ণ জমির বিবরণ: {{PROPERTY_DESCRIPTION}}। উক্ত জমির মালিকানা ও স্বত্বাধিকার: {{OWNERSHIP_TYPE}}।

২। বিরোধের প্রকৃতি: {{DISPUTE_NATURE}}।

৩। ক্ষতির বিবরণ ও সম্ভাব্য আর্থিক ক্ষতি: {{FINANCIAL_LOSS}}।

৪। ঘটনার প্রেক্ষাপট:
{{INCIDENT_DETAILS}}

৫। জমির খতিয়ান, দলিল ও প্রমাণাদির তালিকা:
{{EVIDENCE_LIST}}

প্রার্থনা:
অতএব, বিনীত প্রার্থনা এই যে, বিজ্ঞ আদালত তফসিল বর্ণিত জমিতে দ্বিতীয় পক্ষের বেআইনি হস্তক্ষেপ বন্ধে এবং শান্তিশৃঙ্খলা বজায় রাখিতে আইনানুগ আদেশ প্রদানে মর্জি হয়।

তারিখ: {{APPLICATION_DATE}}
নিবেদক,
{{APPLICANT_NAME}}`,
    supportedPlaceholders: [
      { placeholder: '{{APPLICANT_NAME}}', key: 'APPLICANT_NAME', labelBn: 'আবেদনকারীর নাম', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{RESPONDENT_NAME}}', key: 'RESPONDENT_NAME', labelBn: 'বিবাদীর নাম', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{PROPERTY_DESCRIPTION}}', key: 'PROPERTY_DESCRIPTION', labelBn: 'জমির বিবরণ', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{OWNERSHIP_TYPE}}', key: 'OWNERSHIP_TYPE', labelBn: 'মালিকানার ধরন', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{DISPUTE_NATURE}}', key: 'DISPUTE_NATURE', labelBn: 'বিরোধের প্রকৃতি', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{FINANCIAL_LOSS}}', key: 'FINANCIAL_LOSS', labelBn: 'ক্ষতির পরিমাণ', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{INCIDENT_DETAILS}}', key: 'INCIDENT_DETAILS', labelBn: 'ঘটনার বিবরণ', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{EVIDENCE_LIST}}', key: 'EVIDENCE_LIST', labelBn: 'প্রমাণাদির তালিকা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{DISTRICT}}', key: 'DISTRICT', labelBn: 'জেলা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false }
    ],
    disclaimerNoticeBn: 'সতর্কীকরণ ও আইনি বিজ্ঞপ্তি (DRAFT - PENDING REVIEW): এই আবেদনপত্রটি আবেদনকারীর প্রদত্ত প্রাথমিক ও একতরফা মৌখিক/লিখিত বিবরণের ভিত্তিতে অধিকার (Odhikar) ডিজিটাল সহায়িকা কর্তৃক প্রস্তুতকৃত একটি খসড়া রূপরেখা মাত্র। এতে বর্ণিত অভিযোগ ও তথ্যাদি কোনো আদালত বা তদন্তকারী কর্তৃপক্ষ কর্তৃক চূড়ান্তভাবে যাচাই বা প্রমাণিত নয়। বিজ্ঞ আইনজীবী বা সংশ্লিষ্ট আইনি কর্মকর্তার দ্বারা তথ্যসমূহ যাচাই ও স্বাক্ষর ব্যতীত এটি চূড়ান্ত আইনগত নথি হিসেবে গণ্য হবে না।',
    version: 1,
    isActive: true
  },
  {
    _id: 'bd847f71-6c7a-4433-ab86-24ea33a8d798',
    id: 'bd847f71-6c7a-4433-ab86-24ea33a8d798',
    categoryCode: 'WOMEN_CHILD_PROTECTION',
    titleBn: 'নারী ও শিশু সুরক্ষা ও অধিকার সংরক্ষণ আবেদন',
    titleEn: 'Petition for Protection & Custody of Women and Children',
    recipientAuthorityBn: 'বিজ্ঞ নারী ও শিশু নির্যাতন দমন ট্রাইব্যুনাল / বিজ্ঞ পারিবারিক আদালত, {{DISTRICT}}',
    subjectBn: 'নারী ও শিশুর সুরক্ষা, আইনানুগ অভিভাবকত্ব এবং খোরপোষ নিশ্চিতকরণের দরখাস্ত',
    bodyTemplateBn: `বিজ্ঞ আদালত, {{DISTRICT}}

বিষয়: নারী ও শিশুর আইনগত সুরক্ষা ও অধিকার সংরক্ষণের আবেদন।

আবেদনকারী:
{{APPLICANT_NAME}}

বনাম

বিবাদী:
{{RESPONDENT_NAME}}

মাননীয় আদালতের নিকট বিনীত নিবেদন এই যে,

১। সন্তানের বিবরণ: {{CHILD_DETAILS}}।

২। বঞ্চনা ও অধিকার লঙ্ঘনের প্রকৃতি: {{DEPRIVATION_NATURE}}।

৩। শিশুর নিরাপত্তা ঝুঁকি ও হুমকি: {{CHILD_SAFETY_RISK}}।

৪। কাস্টডি ও অভিভাবকত্ব বিরোধের বিবরণ: {{CUSTODY_DISPUTE}}।

৫। ঘটনার বিস্তারিত বিবরণ:
{{INCIDENT_DETAILS}}

৬। প্রমাণাদি: {{EVIDENCE_LIST}}।

প্রার্থনা:
অতএব, বিনীত প্রার্থনা এই যে, বিজ্ঞ আদালত আবেদনকারী ও শিশুর সুরক্ষায় উপযুক্ত আইনি আদেশ ও খোরপোষ মঞ্জুর করিতে মর্জি হয়।

তারিখ: {{APPLICATION_DATE}}
নিবেদক,
{{APPLICANT_NAME}}`,
    supportedPlaceholders: [
      { placeholder: '{{APPLICANT_NAME}}', key: 'APPLICANT_NAME', labelBn: 'আবেদনকারীর নাম', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{RESPONDENT_NAME}}', key: 'RESPONDENT_NAME', labelBn: 'বিবাদীর নাম', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{CHILD_DETAILS}}', key: 'CHILD_DETAILS', labelBn: 'সন্তানের বিবরণ', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{DEPRIVATION_NATURE}}', key: 'DEPRIVATION_NATURE', labelBn: 'বঞ্চনার প্রকৃতি', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{CHILD_SAFETY_RISK}}', key: 'CHILD_SAFETY_RISK', labelBn: 'নিরাপত্তা ঝুঁকি', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{CUSTODY_DISPUTE}}', key: 'CUSTODY_DISPUTE', labelBn: 'অভিভাবকত্ব বিরোধ', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{INCIDENT_DETAILS}}', key: 'INCIDENT_DETAILS', labelBn: 'ঘটনার বিবরণ', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{EVIDENCE_LIST}}', key: 'EVIDENCE_LIST', labelBn: 'প্রমাণাদির তালিকা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{DISTRICT}}', key: 'DISTRICT', labelBn: 'জেলা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false }
    ],
    disclaimerNoticeBn: 'সতর্কীকরণ ও আইনি বিজ্ঞপ্তি (DRAFT - PENDING REVIEW): এই আবেদনপত্রটি আবেদনকারীর প্রদত্ত প্রাথমিক ও একতরফা মৌখিক/লিখিত বিবরণের ভিত্তিতে অধিকার (Odhikar) ডিজিটাল সহায়িকা কর্তৃক প্রস্তুতকৃত একটি খসড়া রূপরেখা মাত্র। এতে বর্ণিত অভিযোগ ও তথ্যাদি কোনো আদালত বা তদন্তকারী কর্তৃপক্ষ কর্তৃক চূড়ান্তভাবে যাচাই বা প্রমাণিত নয়। বিজ্ঞ আইনজীবী বা সংশ্লিষ্ট আইনি কর্মকর্তার দ্বারা তথ্যসমূহ যাচাই ও স্বাক্ষর ব্যতীত এটি চূড়ান্ত আইনগত নথি হিসেবে গণ্য হবে না।',
    version: 1,
    isActive: true
  },
  {
    _id: 'c8619434-ced8-4301-9e80-652a06894661',
    id: 'c8619434-ced8-4301-9e80-652a06894661',
    categoryCode: 'OTHER',
    titleBn: 'সাধারণ আইনি সহায়তা ও পরামর্শের আবেদন',
    titleEn: 'General Legal Aid & Consultation Application',
    recipientAuthorityBn: 'জেলা লিগ্যাল এইড অফিসার, জেলা লিগ্যাল এইড অফিস, {{DISTRICT}}',
    subjectBn: 'জাতীয় আইনগত সহায়তা প্রদান আইন, ২০০০ অনুযায়ী সরকারি আইনি সহায়তার আবেদন',
    bodyTemplateBn: `জেলা লিগ্যাল এইড অফিসার, জেলা লিগ্যাল এইড অফিস, {{DISTRICT}}

বিষয়: আইনি পরামর্শ ও সহায়তার দরখাস্ত।

আবেদনকারী:
{{APPLICANT_NAME}}

বনাম

অপর পক্ষ / বিবাদী:
{{RESPONDENT_NAME}}

মহোদয়,

বিনীত নিবেদন এই যে, আবেদনকারী নিম্নবর্ণিত আইনি সমস্যায় পতিত হইয়াছেন:

১। ঘটনার বিবরণ:
{{INCIDENT_DETAILS}}

২। প্রার্থিত আইনি সমাধান ও প্রতিকার:
{{DESIRED_RELIEF}}

৩। প্রমাণাদির তালিকা:
{{EVIDENCE_LIST}}

অতএব, বিনীত প্রার্থনা এই যে, আবেদনকারীকে প্রয়োজনীয় আইনি সহায়তা ও সরকারি পরামর্শ প্রদানের ব্যবস্থা গ্রহণে মর্জি হয়।

তারিখ: {{APPLICATION_DATE}}
নিবেদক,
{{APPLICANT_NAME}}`,
    supportedPlaceholders: [
      { placeholder: '{{APPLICANT_NAME}}', key: 'APPLICANT_NAME', labelBn: 'আবেদনকারীর নাম', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{RESPONDENT_NAME}}', key: 'RESPONDENT_NAME', labelBn: 'অপর পক্ষের নাম', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{INCIDENT_DETAILS}}', key: 'INCIDENT_DETAILS', labelBn: 'ঘটনার বিবরণ', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: true },
      { placeholder: '{{DESIRED_RELIEF}}', key: 'DESIRED_RELIEF', labelBn: 'প্রার্থিত প্রতিকার', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{EVIDENCE_LIST}}', key: 'EVIDENCE_LIST', labelBn: 'প্রমাণাদির তালিকা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false },
      { placeholder: '{{DISTRICT}}', key: 'DISTRICT', labelBn: 'জেলা', fallbackBn: '[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]', isRequired: false }
    ],
    disclaimerNoticeBn: 'সতর্কীকরণ ও আইনি বিজ্ঞপ্তি (DRAFT - PENDING REVIEW): এই আবেদনপত্রটি আবেদনকারীর প্রদত্ত প্রাথমিক ও একতরফা মৌখিক/লিখিত বিবরণের ভিত্তিতে অধিকার (Odhikar) ডিজিটাল সহায়িকা কর্তৃক প্রস্তুতকৃত একটি খসড়া রূপরেখা মাত্র। এতে বর্ণিত অভিযোগ ও তথ্যাদি কোনো আদালত বা তদন্তকারী কর্তৃপক্ষ কর্তৃক চূড়ান্তভাবে যাচাই বা প্রমাণিত নয়। বিজ্ঞ আইনজীবী বা সংশ্লিষ্ট আইনি কর্মকর্তার দ্বারা তথ্যসমূহ যাচাই ও স্বাক্ষর ব্যতীত এটি চূড়ান্ত আইনগত নথি হিসেবে গণ্য হবে না।',
    version: 1,
    isActive: true
  }
];

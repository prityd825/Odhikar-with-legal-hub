import { QuestionBankItemDto } from '../../core/paralegal.models';

export const DEFAULT_QUESTION_BANK: Record<string, QuestionBankItemDto[]> = {
  DOWER_MAINTENANCE: [
    { questionId: 'Q-DOWER-001', category: 'applicant', questionBangla: 'আপনার পূর্ণ নাম কী?', requiredFactKey: 'applicant', priority: 10 },
    { questionId: 'Q-DOWER-002', category: 'perpetrator', questionBangla: 'আপনার স্বামীর পূর্ণ নাম কী?', requiredFactKey: 'respondent', priority: 20 },
    { questionId: 'Q-DOWER-003', category: 'incident_time', questionBangla: 'আপনাদের বিবাহের সঠিক তারিখ বা সাল কোনটি?', requiredFactKey: 'marriageDate', priority: 30 },
    { questionId: 'Q-DOWER-004', category: 'financial_details', questionBangla: 'কাবিননামা অনুযায়ী আপনার দেনমোহরের মোট ধার্যকৃত অঙ্ক কত টাকা?', requiredFactKey: 'dowerAmount', priority: 40 },
    { questionId: 'Q-DOWER-005', category: 'financial_details', questionBangla: 'বিবাহের সময় বা পরবর্তীতে আপনাকে কত টাকা দেনমোহর বাবদ পরিশোধ করা হয়েছে?', requiredFactKey: 'paidDowerAmount', priority: 50 },
    { questionId: 'Q-DOWER-006', category: 'financial_details', questionBangla: 'বর্তমানে আপনার বকেয়া দেনমোহরের মোট পরিমাণ কত টাকা?', requiredFactKey: 'unpaidDowerAmount', priority: 60 },
    { questionId: 'Q-DOWER-007', category: 'financial_details', questionBangla: 'স্বামী খোরপোষ না দেওয়ার কারণে আপনার কত টাকা বকেয়া ভরণপোষণ পাওনা রয়েছে?', requiredFactKey: 'maintenanceDue', priority: 70 },
    { questionId: 'Q-DOWER-008', category: 'financial_details', questionBangla: 'আপনার সন্তানদের সংখ্যা কত এবং তাদের জন্য প্রতি মাসে কত টাকা ভরণপোষণ দাবি করছেন?', requiredFactKey: 'childCount', priority: 80 },
    { questionId: 'Q-DOWER-009', category: 'evidence_documents', questionBangla: 'আপনার নিকট কী কী প্রমাণাদি বা নথিপত্র (যেমন: কাবিননামা, শালিসনামা) সংরক্ষিত আছে?', requiredFactKey: 'evidenceList', priority: 90 },
    { questionId: 'Q-DOWER-010', category: 'desired_relief', questionBangla: 'এই বিষয়ে আপনি কী ধরনের আইনি প্রতিকার (যেমন: পারিবারিক আদালতে মামলা বা আপস) চাচ্ছেন?', requiredFactKey: 'desiredRelief', priority: 100 }
  ],
  DOWRY_DOMESTIC_VIOLENCE: [
    { questionId: 'Q-DOWRY-001', category: 'applicant', questionBangla: 'নির্যাতনের শিকার ব্যক্তি বা আবেদনকারীর পূর্ণ নাম কী?', requiredFactKey: 'applicant', priority: 10 },
    { questionId: 'Q-DOWRY-002', category: 'perpetrator', questionBangla: 'অভিযুক্ত ব্যক্তি বা নির্যাতনকারীর পূর্ণ নাম কী?', requiredFactKey: 'respondent', priority: 20 },
    { questionId: 'Q-DOWRY-003', category: 'financial_details', questionBangla: 'যৌতুক হিসেবে সুনির্দিষ্টভাবে কত টাকা বা কী কী মালামাল দাবি করা হয়েছে?', requiredFactKey: 'dowryDemandAmount', priority: 30 },
    { questionId: 'Q-DOWER-004', category: 'financial_details', questionBangla: 'যৌতুক বাবদ পূর্বে ইতোমধ্যে কত টাকা বা মালামাল প্রদান করা হয়েছিল?', requiredFactKey: 'dowryPaidAmount', priority: 40 },
    { questionId: 'Q-DOWRY-005', category: 'incident_description', questionBangla: 'আপনার ওপর মূলত কী ধরনের শারীরিক বা মানসিক নির্যাতন চালানো হয়েছে?', requiredFactKey: 'abuseType', priority: 50 },
    { questionId: 'Q-DOWRY-006', category: 'injury_details', questionBangla: 'শারীরিক আঘাতের বিবরণ এবং হাসপাতালে নেওয়া কোনো চিকিৎসার তথ্য উল্লেখ করুন।', requiredFactKey: 'injuryDetails', priority: 60 },
    { questionId: 'Q-DOWRY-007', category: 'incident_location', questionBangla: 'নির্যাতন বা বাড়ি থেকে তাড়িয়ে দেওয়ার ঘটনাটি সুনির্দিষ্টভাবে কোথায় ঘটেছে?', requiredFactKey: 'incidentLocation', priority: 70 },
    { questionId: 'Q-DOWRY-008', category: 'current_safety', questionBangla: 'বর্তমানে আপনার ওপর কোনো জরুরি শারীরিক ক্ষতি বা জীবননাশের হুমকি আছে কি?', requiredFactKey: 'immediateThreat', priority: 80 },
    { questionId: 'Q-DOWRY-009', category: 'evidence_documents', questionBangla: 'এই বিষয়ে স্থানীয় থানায় কোনো জিডি (GD) বা লিখিত অভিযোগ দায়ের করা হয়েছে কি?', requiredFactKey: 'policeReport', priority: 90 },
    { questionId: 'Q-DOWRY-010', category: 'witness_details', questionBangla: 'ঘটনার কোনো প্রত্যক্ষদর্শী সাক্ষী বা অন্য কোনো প্রমাণাদি আছে কি?', requiredFactKey: 'witnessList', priority: 100 }
  ],
  LABOUR_WAGE: [
    { questionId: 'Q-WAGE-001', category: 'applicant', questionBangla: 'আপনার (শ্রমিক/কর্মচারী) পূর্ণ নাম কী?', requiredFactKey: 'applicant', priority: 10 },
    { questionId: 'Q-WAGE-002', category: 'perpetrator', questionBangla: 'আপনার মালিক বা প্রতিষ্ঠানের নাম এবং প্রতিষ্ঠানের পূর্ণ ঠিকানা কী?', requiredFactKey: 'employerName', priority: 20 },
    { questionId: 'Q-WAGE-003', category: 'employment_details', questionBangla: 'উক্ত প্রতিষ্ঠানে আপনার পদবী (Designation) বা কাজের বিবরণ কী ছিল?', requiredFactKey: 'jobDesignation', priority: 30 },
    { questionId: 'Q-WAGE-004', category: 'financial_details', questionBangla: 'আপনার মাসিক মূল মজুরি বা দৈনিক হাজিরা হার কত ছিল?', requiredFactKey: 'monthlySalary', priority: 40 },
    { questionId: 'Q-WAGE-005', category: 'financial_details', questionBangla: 'মোট কত মাস বা কতদিনের বেতন বকেয়া রয়েছে?', requiredFactKey: 'unpaidMonths', priority: 50 },
    { questionId: 'Q-WAGE-006', category: 'financial_details', questionBangla: 'প্রতিষ্ঠানটির নিকট আপনার মোট বকেয়া মজুরির দাবি কত টাকা?', requiredFactKey: 'unpaidWageAmount', priority: 60 },
    { questionId: 'Q-WAGE-007', category: 'financial_details', questionBangla: 'আপনার কোনো বকেয়া ওভারটাইম ভাতা পাওনা থাকলে তার পরিমাণ কত?', requiredFactKey: 'overtimeDue', priority: 70 },
    { questionId: 'Q-WAGE-008', category: 'incident_time', questionBangla: 'আপনাকে চাকরি থেকে অব্যাহতি বা বরখাস্ত করা হয়ে থাকলে তার তারিখ ও কারণ কী?', requiredFactKey: 'terminationDetails', priority: 80 },
    { questionId: 'Q-WAGE-009', category: 'evidence_documents', questionBangla: 'আপনার নিকট কী কী প্রামাণ্য নথি (যেমন: নিয়োগপত্র, আইডি কার্ড, পে-স্লিপ, হাজিরা খাতা) রয়েছে?', requiredFactKey: 'evidenceList', priority: 90 }
  ],
  LAND_PROPERTY: [
    { questionId: 'Q-LAND-001', category: 'applicant', questionBangla: 'বঞ্চিত স্বত্বাধিকারী বা আবেদনকারীর পূর্ণ নাম কী?', requiredFactKey: 'applicant', priority: 10 },
    { questionId: 'Q-LAND-002', category: 'perpetrator', questionBangla: 'সম্পত্তি দখলদার বা অভিযুক্ত প্রতিপক্ষের পূর্ণ নাম কী?', requiredFactKey: 'respondent', priority: 20 },
    { questionId: 'Q-LAND-003', category: 'property_details', questionBangla: 'বিরোধপূর্ণ জমির তফসিল (মৌজা, খতিয়ান, দাগ নম্বর ও জমির পরিমাণ) উল্লেখ করুন।', requiredFactKey: 'propertyDescription', priority: 30 },
    { questionId: 'Q-LAND-004', category: 'ownership_details', questionBangla: 'জমিটিতে আপনার মালিকানার প্রধান সূত্র কী (যেমন: পৈতৃক, ক্রয়, হেবা নাকি দেনমোহর)?', requiredFactKey: 'ownershipType', priority: 40 },
    { questionId: 'Q-LAND-005', category: 'incident_description', questionBangla: 'আপনাকে কীভাবে অধিকার বঞ্চিত বা বেদখল করা হয়েছে তার বিবরণ দিন।', requiredFactKey: 'deprivationNature', priority: 50 },
    { questionId: 'Q-LAND-006', category: 'financial_details', questionBangla: 'উক্ত সম্পত্তিতে আপনার আইনগত প্রাপ্য হিস্যা বা অংশের পরিমাণ কত?', requiredFactKey: 'inheritanceShare', priority: 60 },
    { questionId: 'Q-LAND-007', category: 'current_safety', questionBangla: 'জমি জোরপূর্বক লিখে নেওয়ার জন্য কোনো হুমকি বা চাপ দেওয়া হচ্ছে কি?', requiredFactKey: 'threatDetails', priority: 70 },
    { questionId: 'Q-LAND-008', category: 'evidence_documents', questionBangla: 'জমির স্বপক্ষে আপনার নিকট কী কী দলিল-দস্তাবেজ (যেমন: খতিয়ান, পরচা, খাজনা দাখিলা) রয়েছে?', requiredFactKey: 'evidenceList', priority: 80 },
    { questionId: 'Q-LAND-009', category: 'witness_details', questionBangla: 'এই জমি সংক্রান্ত বিষয়ে পূর্বে কোনো স্থানীয় শালিস বা মীমাংসার উদ্যোগ নেওয়া হয়েছিল কি?', requiredFactKey: 'localArbitration', priority: 90 }
  ],
  WOMEN_CHILD_PROTECTION: [
    { questionId: 'Q-PROT-001', category: 'applicant', questionBangla: 'আবেদনকারী মা বা অভিভাবকের পূর্ণ নাম কী?', requiredFactKey: 'applicant', priority: 10 },
    { questionId: 'Q-PROT-002', category: 'perpetrator', questionBangla: 'সন্তানে বা চলাফেরায় বাধা প্রদানকারী প্রতিপক্ষের পূর্ণ নাম কী?', requiredFactKey: 'respondent', priority: 20 },
    { questionId: 'Q-PROT-003', category: 'child_details', questionBangla: 'সন্তানদের নাম, বয়স এবং তারা বর্তমানে কার কাছে বা কোথায় অবস্থান করছে?', requiredFactKey: 'childDetails', priority: 30 },
    { questionId: 'Q-PROT-004', category: 'incident_description', questionBangla: 'সন্তানদের হেফাজত বা তাদের সাথে দেখা-সাক্ষাৎ করার ক্ষেত্রে কী ধরনের বাধা দেওয়া হচ্ছে?', requiredFactKey: 'custodyDisputeNature', priority: 40 },
    { questionId: 'Q-PROT-005', category: 'injury_details', questionBangla: 'আপনার বা আপনার সন্তানকে প্রয়োজনীয় চিকিৎসা সেবা নিতে কীভাবে বাধা দেওয়া হয়েছে?', requiredFactKey: 'medicalObstruction', priority: 50 },
    { questionId: 'Q-PROT-006', category: 'current_safety', questionBangla: 'আপনাকে কি অন্যায়ভাবে গৃহবন্দি বা চলাফেরায় কোনো কঠোর নিষেধাজ্ঞা আরোপ করা হয়েছে?', requiredFactKey: 'movementRestriction', priority: 60 },
    { questionId: 'Q-PROT-007', category: 'financial_details', questionBangla: 'মৌলিক চাহিদা বা জীবনযাত্রার প্রয়োজনীয় খরচ না দিয়ে কীভাবে আর্থিক চাপ দেওয়া হচ্ছে?', requiredFactKey: 'economicControl', priority: 70 },
    { questionId: 'Q-PROT-008', category: 'current_safety', questionBangla: 'সন্তানরা বর্তমানে শারীরিক বা মানসিকভাবে কোনো তাৎক্ষণিক নিরাপত্তা ঝুঁকিতে আছে কি?', requiredFactKey: 'childSafetyRisk', priority: 80 },
    { questionId: 'Q-PROT-009', category: 'desired_relief', questionBangla: 'আদালতের নিকট আপনি সন্তানদের হেফাজত ও সুরক্ষা সংক্রান্ত কী আদেশ চাচ্ছেন?', requiredFactKey: 'desiredRelief', priority: 90 }
  ],
  OTHER: [
    { questionId: 'Q-OTHER-001', category: 'applicant', questionBangla: 'আপনার পূর্ণ নাম কী?', requiredFactKey: 'applicant', priority: 10 },
    { questionId: 'Q-OTHER-002', category: 'perpetrator', questionBangla: 'বিবাদী বা অপর পক্ষের পূর্ণ নাম কী?', requiredFactKey: 'respondent', priority: 20 },
    { questionId: 'Q-OTHER-003', category: 'incident_description', questionBangla: 'আপনার সাথে ঘটে যাওয়া মূল সমস্যা বা ঘটনার বিবরণ সংক্ষেপে লিখুন।', requiredFactKey: 'incidentDescription', priority: 30 },
    { questionId: 'Q-OTHER-004', category: 'incident_time', questionBangla: 'ঘটনাটি কত তারিখে এবং কোন স্থানে সংঘটিত হয়েছিল?', requiredFactKey: 'incidentDate', priority: 40 },
    { questionId: 'Q-OTHER-005', category: 'incident_details', questionBangla: 'ঘটনার মধ্যে কোনো মিথ্যা তথ্য বা প্রতারণা (যেমন: বৈবাহিক তথ্য গোপন) থাকলে তা উল্লেখ করুন।', requiredFactKey: 'fraudDetails', priority: 50 },
    { questionId: 'Q-OTHER-006', category: 'financial_details', questionBangla: 'এই ঘটনার কারণে আপনার আনুমানিক কত টাকার আর্থিক বা মানসিকভাবে ক্ষতি হয়েছে?', requiredFactKey: 'financialLoss', priority: 60 },
    { questionId: 'Q-OTHER-007', category: 'evidence_documents', questionBangla: 'আপনার দাবির পক্ষে কোনো কাগজপত্র বা লিখিত প্রমাণাদি আছে কি?', requiredFactKey: 'evidenceList', priority: 70 },
    { questionId: 'Q-OTHER-008', category: 'desired_relief', questionBangla: 'এই আইনি সমস্যা সমাধানে আপনি আমাদের নিকট থেকে কী ধরনের সহায়তা আশা করছেন?', requiredFactKey: 'desiredRelief', priority: 80 }
  ]
};

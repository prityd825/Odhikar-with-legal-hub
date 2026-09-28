export interface LegalUserProfile {
  id: string;
  nameBangla: string;
  nameEnglish: string;
  designationBangla: string;
  designationEnglish: string;
  role: 'panel_advocate' | 'paralegal_officer' | 'legal_aid_officer';
  sanadNumber: string;
  barAssociation: string;
  practicingCourts: string;
  specializationBangla: string[];
  enrolmentYear: string;
  chamberAddress: string;
  officialEmail: string;
  phone: string;
  verifiedStatus: string;
  avatarInitials: string;
  casesAssigned: number;
  casesApprovedToday: number;
  accuracyScore: string;
}

export const DEMO_LEGAL_PROFILES: LegalUserProfile[] = [
  {
    id: 'adv-farhana',
    nameBangla: 'এডভোকেট ফারহানা চৌধুরী',
    nameEnglish: 'Advocate Farhana Chowdhury, LL.M',
    designationBangla: 'সিনিয়র প্যানেল আইনজীবী ও লিগ্যাল এইড কনসালট্যান্ট',
    designationEnglish: 'Senior Panel Advocate & Legal Aid Consultant',
    role: 'panel_advocate',
    sanadNumber: 'BD-BAR/DHAKA/2018/14290',
    barAssociation: 'ঢাকা আইনজীবী সমিতি (Dhaka Bar Association)',
    practicingCourts: 'ঢাকা জেলা ও দায়রা জজ আদালত, নারী ও শিশু নির্যাতন দমন ট্রাইব্যুনাল',
    specializationBangla: [
      'পারিবারিক আদালত আইন ২০২৩ (দেনমোহর/ভরণপোষণ)',
      'যৌতুক ও পারিবারিক সহিংসতা নিরোধ আইন',
      'নারী ও শিশু সুরক্ষা ও অন্তর্বর্তীকালীন হেফাজত',
      'বিকল্প বিরোধ নিষ্পত্তি (ADR)'
    ],
    enrolmentYear: '২০১৮ (তালিকাভুক্ত অ্যাডভোকেট)',
    chamberAddress: 'অ্যানেক্স ভবন-৩, ঢাকা বার কাউন্সিল সংলগ্ন, ঢাকা জজ কোর্ট প্রাঙ্গণ',
    officialEmail: 'advocate.farhana@odhikar.law.bd',
    phone: '+880 1711-889922',
    verifiedStatus: 'বাংলাদেশ বার কাউন্সিল ভেরিফাইড (Demo)',
    avatarInitials: 'এফসি',
    casesAssigned: 18,
    casesApprovedToday: 4,
    accuracyScore: '৯৮.৫%'
  },
  {
    id: 'para-tanveer',
    nameBangla: 'তানভীর আহমেদ (লিগ্যাল অফিসার)',
    nameEnglish: 'Tanveer Ahmed, LL.B',
    designationBangla: 'প্যারালেগাল ও কেস ড্রাফটিং অফিসার',
    designationEnglish: 'Paralegal & Legal Case Officer',
    role: 'paralegal_officer',
    sanadNumber: 'NLASO-PL-2022-0471',
    barAssociation: 'জাতীয় আইনগত সহায়তা প্রদান সংস্থা (NLASO)',
    practicingCourts: 'পারিবারিক ও মেট্রোপলিটন ম্যাজিস্ট্রেট আদালত, ঢাকা',
    specializationBangla: [
      'প্রাথমিক কেস স্ক্রিনিং ও ফ্যাক্ট-ফাইন্ডিং',
      'আইনি নোটিশ ও খসড়া দরখাস্ত নিরীক্ষা',
      'ক্লায়েন্ট কাউন্সেলিং ও মিডিয়েশন সহায়তা'
    ],
    enrolmentYear: '২০২২ (NLASO সার্টিফাইড প্যারালেগাল)',
    chamberAddress: 'জেলা লিগ্যাল এইড অফিস, ঢাকা জেলা আদালত ভবন',
    officialEmail: 'tanveer.paralegal@odhikar.law.bd',
    phone: '+880 1822-445566',
    verifiedStatus: 'NLASO সার্টিফাইড প্যারালেগাল (Demo)',
    avatarInitials: 'তাআ',
    casesAssigned: 12,
    casesApprovedToday: 2,
    accuracyScore: '৯৭.২%'
  }
];

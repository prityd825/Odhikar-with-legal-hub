import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { DEMO_LEGAL_PROFILES, LegalUserProfile } from '../../core/auth.models';
import { LanguageService } from '../../core/language.service';

@Component({
  selector: 'app-paralegal-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './paralegal-login.component.html',
  styleUrls: ['./paralegal-login.component.css']
})
export class ParalegalLoginComponent {
  public langService = inject(LanguageService);
  private router = inject(Router);
  private authService = inject(AuthService);

  profiles: LegalUserProfile[] = DEMO_LEGAL_PROFILES;
  selectedProfileId: string = 'adv-farhana';

  // Form Fields
  sanadNumber: string = 'BD-BAR/DHAKA/2018/14290';
  pinCode: string = '123456';
  selectedCourt: string = 'ঢাকা জেলা ও দায়রা জজ আদালত';
  courtOptions: string[] = [
    'ঢাকা জেলা ও দায়রা জজ আদালত',
    'পারিবারিক আদালত ও মহানগর হাকিম আদালত, ঢাকা',
    'নারী ও শিশু নির্যাতন দমন ট্রাইব্যুনাল, ঢাকা',
    'জাতীয় আইনগত সহায়তা প্রদান সংস্থা (NLASO) কেন্দ্রীয় ডেস্ক'
  ];

  isLoggingIn: boolean = false;
  loginError: string = '';

  get currentSelectedProfile(): LegalUserProfile {
    return this.profiles.find((p: LegalUserProfile) => p.id === this.selectedProfileId) || this.profiles[0];
  }

  selectProfile(id: string): void {
    this.selectedProfileId = id;
    const p = this.currentSelectedProfile;
    this.sanadNumber = p.sanadNumber;
    this.pinCode = '123456';
    this.loginError = '';
  }

  handleLogin(): void {
    this.isLoggingIn = true;
    this.loginError = '';

    setTimeout(() => {
      this.authService.loginWithProfile(this.selectedProfileId);
      this.isLoggingIn = false;
      this.router.navigate(['/paralegal']);
    }, 600);
  }

  quickDemoLogin(profileId: string): void {
    this.selectedProfileId = profileId;
    this.authService.loginWithProfile(profileId);
    this.router.navigate(['/paralegal']);
  }
}

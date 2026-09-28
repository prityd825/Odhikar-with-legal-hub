import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { LegalUserProfile, DEMO_LEGAL_PROFILES } from './auth.models';

export * from './auth.models';

export interface IAuthService {
  currentUser: LegalUserProfile;
  isLoggedIn(): boolean;
  loginWithProfile(profileId: string): boolean;
  logout(): void;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService implements IAuthService {
  private readonly STORAGE_USER_KEY = 'odhikar_paralegal_user';
  private readonly STORAGE_AUTH_KEY = 'odhikar_paralegal_is_logged_in';

  public currentUser: LegalUserProfile = DEMO_LEGAL_PROFILES[0];
  private currentUserSubject: BehaviorSubject<LegalUserProfile>;
  public currentUser$: Observable<LegalUserProfile>;

  private loggedInSubject: BehaviorSubject<boolean>;
  public loggedIn$: Observable<boolean>;

  constructor() {
    const savedUser = this.loadUserFromStorage();
    const isAuth = this.loadAuthFromStorage();

    const initialUser = savedUser || DEMO_LEGAL_PROFILES[0];
    this.currentUser = initialUser;
    this.currentUserSubject = new BehaviorSubject<LegalUserProfile>(initialUser);
    this.currentUser$ = this.currentUserSubject.asObservable();

    this.loggedInSubject = new BehaviorSubject<boolean>(isAuth);
    this.loggedIn$ = this.loggedInSubject.asObservable();
  }

  public isLoggedIn(): boolean {
    return this.loggedInSubject.value;
  }

  public loginWithProfile(profileId: string): boolean {
    const matched = DEMO_LEGAL_PROFILES.find((p: LegalUserProfile) => p.id === profileId) || DEMO_LEGAL_PROFILES[0];
    this.currentUser = matched;
    this.saveUserToStorage(matched);
    this.currentUserSubject.next(matched);
    this.setAuthToStorage(true);
    this.loggedInSubject.next(true);
    return true;
  }

  public logout(): void {
    this.setAuthToStorage(false);
    this.loggedInSubject.next(false);
  }

  private loadAuthFromStorage(): boolean {
    try {
      return localStorage.getItem(this.STORAGE_AUTH_KEY) === 'true';
    } catch {
      return false;
    }
  }

  private setAuthToStorage(val: boolean): void {
    try {
      if (val) {
        localStorage.setItem(this.STORAGE_AUTH_KEY, 'true');
      } else {
        localStorage.removeItem(this.STORAGE_AUTH_KEY);
      }
    } catch {
      // ignore
    }
  }

  private loadUserFromStorage(): LegalUserProfile | null {
    try {
      const raw = localStorage.getItem(this.STORAGE_USER_KEY);
      if (raw) {
        return JSON.parse(raw) as LegalUserProfile;
      }
    } catch {
      // ignore
    }
    return null;
  }

  private saveUserToStorage(profile: LegalUserProfile): void {
    try {
      localStorage.setItem(this.STORAGE_USER_KEY, JSON.stringify(profile));
    } catch {
      // ignore
    }
  }
}

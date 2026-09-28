import { Routes } from '@angular/router';
import { WelcomeComponent } from './welcome/welcome.component';
import { IntakeComponent } from './intake/intake.component';
import { ParalegalComponent } from './paralegal/paralegal.component';
import { ParalegalLoginComponent } from './auth/paralegal-login/paralegal-login.component';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', component: WelcomeComponent },
  { path: 'intake', component: IntakeComponent },
  { path: 'client', redirectTo: 'intake', pathMatch: 'full' },
  { path: 'paralegal/login', component: ParalegalLoginComponent },
  { path: 'paralegal', component: ParalegalComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '' }
];
 
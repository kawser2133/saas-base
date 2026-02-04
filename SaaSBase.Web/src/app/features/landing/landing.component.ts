import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { isAuthenticated } from '../../core/auth/auth.utils';
import { PublicHeaderComponent } from '../../shared/components/public-header/public-header.component';
import { PublicFooterComponent } from '../../shared/components/public-footer/public-footer.component';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, RouterModule, PublicHeaderComponent, PublicFooterComponent],
  templateUrl: './landing.component.html',
  styleUrls: ['./landing.component.scss']
})
export class LandingComponent implements OnInit {
  constructor(private router: Router) {}

  ngOnInit(): void {
    // Redirect to dashboard if already authenticated
    if (isAuthenticated()) {
      this.router.navigate(['/dashboard'], { replaceUrl: true });
    }
  }

  getStarted(): void {
    this.router.navigate(['/setup']);
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }
}


import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { BrandLogoComponent } from '../brand-logo/brand-logo.component';

@Component({
  selector: 'app-public-footer',
  standalone: true,
  imports: [CommonModule, RouterModule, BrandLogoComponent],
  templateUrl: './public-footer.component.html',
  styleUrls: ['./public-footer.component.scss']
})
export class PublicFooterComponent {
  currentYear = new Date().getFullYear();
}


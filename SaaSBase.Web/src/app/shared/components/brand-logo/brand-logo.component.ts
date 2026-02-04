import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-brand-logo',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './brand-logo.component.html',
  styleUrls: ['./brand-logo.component.scss']
})
export class BrandLogoComponent {
  @Input() size: 'sm' | 'md' | 'lg' = 'md';
  @Input() showText: boolean = true;
  @Input() linkTo: string = '/';
  @Input() variant: 'default' | 'gradient' = 'default';
}
